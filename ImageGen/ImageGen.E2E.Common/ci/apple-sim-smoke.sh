#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 3 ]; then
  echo "usage: $0 <app.tar.gz> <bundle-id> <out-dir>" >&2
  exit 2
fi

artifact=$1
bundle_id=$2
out_dir=$3
script_dir=$(cd "$(dirname "$0")" && pwd)
repo_root=$(cd "$script_dir/../../.." && pwd)
mkdir -p "$out_dir"
out_dir=$(cd "$out_dir" && pwd)
started_epoch=$(date +%s)
touch "$out_dir/run-start.marker"
run_id=${GITHUB_RUN_ID:-local-$$}
udid=''
extract_dir=''
runtime_name=unknown
device_name=unknown
executable=unknown
launched=false
alive=false
crash_count=0
other_crash_count=0
launch_pid=''
launch_started_monotonic=''
exited_at_seconds=null
first_seen_at_seconds=null
result=1

copy_crash_reports() {
  mkdir -p "$out_dir/crash-reports/app" "$out_dir/crash-reports/other"
  local sources=("$HOME/Library/Logs/DiagnosticReports" "$HOME/Library/Developer/CoreSimulator/Devices/$udid/data/Library/Logs/CrashReporter")
  local source report destination
  for source in "${sources[@]}"; do
    [ -d "$source" ] || continue
    while IFS= read -r -d '' report; do
      if python3 - "$report" "$executable" <<'PY'
import json
import pathlib
import re
import sys

path = pathlib.Path(sys.argv[1])
expected = sys.argv[2]
try:
    text = path.read_text(encoding="utf-8", errors="replace")
except OSError:
    text = ""
matched = False
try:
    data = json.loads(text)
    matched = any(data.get(key) == expected for key in ("procName", "app_name", "name", "process"))
except (json.JSONDecodeError, AttributeError):
    matched = bool(re.search(r"^Process:\s+" + re.escape(expected) + r"(?:\s|$)", text, re.MULTILINE))
sys.exit(0 if matched else 1)
PY
      then
        destination="$out_dir/crash-reports/app"
      else
        destination="$out_dir/crash-reports/other"
      fi
      cp "$report" "$destination/"
    done < <(find "$source" -type f \( -name '*.crash' -o -name '*.ips' \) -newer "$out_dir/run-start.marker" -print0 2>/dev/null || true)
  done
  crash_count=$(find "$out_dir/crash-reports/app" -type f | wc -l | tr -d ' ')
  other_crash_count=$(find "$out_dir/crash-reports/other" -type f | wc -l | tr -d ' ')
}

copy_app_logs() {
  local data_container log_file
  data_container=$(xcrun simctl get_app_container "$udid" "$bundle_id" data 2>/dev/null) || {
    echo "Could not locate the app data container for $bundle_id" >"$out_dir/app-logs.txt"
    return 0
  }
  mkdir -p "$out_dir/app-logs"
  while IFS= read -r -d '' log_file; do
    cp "$log_file" "$out_dir/app-logs/"
  done < <(find "$data_container" -type f -name 'imagegen-*.log' -print0 2>/dev/null || true)
  if [ -z "$(find "$out_dir/app-logs" -type f -print -quit 2>/dev/null)" ]; then
    echo "No imagegen-*.log files found under the app data container: $data_container" >"$out_dir/app-logs.txt"
  fi
}

write_summary() {
  python3 - "$out_dir/summary.json" "$runtime_name" "$device_name" "$launched" "$alive" "$crash_count" "$other_crash_count" "$first_seen_at_seconds" "$exited_at_seconds" <<'PY'
import json
import sys

keys = ("runtime", "deviceType", "launched", "aliveAfter30s", "crashReports")
values = sys.argv[2:]
summary = dict(zip(keys, values))
summary["launched"] = summary["launched"] == "true"
summary["aliveAfter30s"] = summary["aliveAfter30s"] == "true"
summary["crashReports"] = int(summary["crashReports"])
summary["otherCrashReports"] = int(sys.argv[7])
summary["firstSeenAtSeconds"] = None if sys.argv[8] == "null" else int(sys.argv[8])
summary["exitedAtSeconds"] = None if sys.argv[9] == "null" else int(sys.argv[9])
with open(sys.argv[1], "w", encoding="utf-8") as output:
    json.dump(summary, output, indent=2)
    output.write("\n")
PY
}

cleanup() {
  local exit_code=$?
  local simulator_predicate
  set +e
  if [ -n "$launch_pid" ]; then
    kill "$launch_pid" >/dev/null 2>&1
    wait "$launch_pid" >/dev/null 2>&1
  fi
  if [ -n "$udid" ]; then
    copy_app_logs
    simulator_predicate="process == \"$executable\" OR process == \"runningboardd\" OR process == \"SpringBoard\" OR process == \"FrontBoard\""
    if command -v gtimeout >/dev/null 2>&1; then
      gtimeout 60 xcrun simctl spawn "$udid" log show --last 10m --predicate "$simulator_predicate" --style compact >"$out_dir/simulator.log" 2>&1 || echo "simulator log collection timed out or failed" >>"$out_dir/simulator.log"
    else
      xcrun simctl spawn "$udid" log show --last 10m --predicate "$simulator_predicate" --style compact >"$out_dir/simulator.log" 2>&1 || echo "simulator log collection failed" >>"$out_dir/simulator.log"
    fi
    if [ ! -s "$out_dir/simulator.log" ]; then
      printf 'No simulator log entries were returned for predicate: %s\n' "$simulator_predicate" >"$out_dir/simulator.log"
    fi
    copy_crash_reports
    xcrun simctl shutdown "$udid" >/dev/null 2>&1
    xcrun simctl delete "$udid" >/dev/null 2>&1
  fi
  if [ -n "$extract_dir" ]; then
    rm -rf "$extract_dir"
  fi
  write_summary
  if [ "$exit_code" -ne 0 ]; then
    exit "$exit_code"
  fi
  exit "$result"
}
trap cleanup EXIT

if [ ! -f "$artifact" ]; then
  echo "application artifact not found: $artifact" >&2
  exit 1
fi

xcrun simctl list -j runtimes >"$out_dir/runtimes.json"
xcrun simctl list -j devicetypes >"$out_dir/devicetypes.json"
if [ -n "${APPLE_SIM_RUNTIME_VERSION:-}" ]; then
  selection=$(python3 -I "$script_dir/apple-sim-pick.py" "$out_dir/runtimes.json" "$out_dir/devicetypes.json" "$APPLE_SIM_RUNTIME_VERSION")
else
  selection=$(python3 -I "$script_dir/apple-sim-pick.py" "$out_dir/runtimes.json" "$out_dir/devicetypes.json")
fi
runtime=$(python3 -c 'import json,sys; print(json.loads(sys.argv[1])["runtime"])' "$selection")
runtime_name=$(python3 -c 'import json,sys; print(json.loads(sys.argv[1])["runtimeName"])' "$selection")
device_type=$(python3 -c 'import json,sys; print(json.loads(sys.argv[1])["deviceType"])' "$selection")
device_name=$(python3 -c 'import json,sys; print(json.loads(sys.argv[1])["deviceName"])' "$selection")
printf '%s\n' "$selection" >"$out_dir/selection.json"

udid=$(xcrun simctl create "e2e-$run_id" "$device_type" "$runtime")
xcrun simctl boot "$udid"
boot_deadline=$(( $(date +%s) + 300 ))
until xcrun simctl list devices available | grep -F "$udid" | grep -q Booted; do
  if [ "$(date +%s)" -ge "$boot_deadline" ]; then
    echo "simulator boot timed out after 300 seconds" >&2
    exit 1
  fi
  sleep 5
done
xcrun simctl bootstatus "$udid" -b >"$out_dir/boot.log" 2>&1

extract_dir="$repo_root/tmp/apple-sim-smoke-$run_id-$$"
mkdir -p "$extract_dir"
tar -xzf "$artifact" -C "$extract_dir"
app_path=$(find "$extract_dir" -type d -name '*.app' -print -quit)
if [ -z "$app_path" ]; then
  echo "archive contains no .app bundle" >&2
  exit 1
fi
actual_bundle=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$app_path/Info.plist")
if [ "$actual_bundle" != "$bundle_id" ]; then
  echo "bundle id mismatch: expected $bundle_id, got $actual_bundle" >&2
  exit 1
fi
executable=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleExecutable' "$app_path/Info.plist")
architectures=$(lipo -archs "$app_path/$executable")
if ! grep -qw arm64 <<<"$architectures"; then
  echo "application executable does not contain arm64: $architectures" >&2
  exit 1
fi

xcrun simctl install "$udid" "$app_path"
launch_started_monotonic=$(python3 -c 'import time; print(time.monotonic())')
xcrun simctl launch --console-pty "$udid" "$bundle_id" >"$out_dir/launch.log" 2>&1 &
launch_pid=$!
launched=true
timeline_result=$(python3 - "$udid" "$bundle_id" "$out_dir" "$launch_started_monotonic" <<'PY'
import datetime
import json
import math
import pathlib
import subprocess
import sys
import time

udid, bundle_id, out_dir, launch_started = sys.argv[1:]
out_dir = pathlib.Path(out_dir)
launch_started = float(launch_started)
first_seen_at_seconds = None
exited_at_seconds = None
seen_running_by_30 = False
exited_by_30 = False
unconfirmed_probe_failure_by_30 = False
screenshots = []

with (out_dir / "process-timeline.txt").open("w", encoding="utf-8") as timeline:
    timeline.write("elapsedSeconds\ttimestampUtc\trunning\tlaunchctlEntry\tprobeStatus\n")
    scheduled_seconds = 5
    while scheduled_seconds <= 60:
        delay = launch_started + scheduled_seconds - time.monotonic()
        if delay > 0:
            time.sleep(delay)

        if scheduled_seconds in (15, 30):
            screenshot_name = "screenshot.png" if scheduled_seconds == 15 else "screenshot-30s.png"
            try:
                screenshots.append(subprocess.Popen(
                    ["xcrun", "simctl", "io", udid, "screenshot", str(out_dir / screenshot_name)],
                    stdout=subprocess.DEVNULL,
                    stderr=subprocess.DEVNULL,
                ))
            except OSError:
                pass

        try:
            probe = subprocess.run(
                ["xcrun", "simctl", "spawn", udid, "launchctl", "list"],
                check=False,
                capture_output=True,
                text=True,
                timeout=4,
            )
            probe_succeeded = probe.returncode == 0
            if probe_succeeded:
                launchctl_entry = next(
                    (line.strip() for line in probe.stdout.splitlines() if bundle_id in line),
                    "",
                )
                probe_status = "ok"
            else:
                detail = probe.stderr.strip() or f"exit code {probe.returncode}"
                launchctl_entry = f"probe failed: {detail}"
                probe_status = "error"
        except subprocess.TimeoutExpired:
            probe_succeeded = False
            launchctl_entry = "probe timed out after 4s"
            probe_status = "timeout"
        except OSError as error:
            probe_succeeded = False
            launchctl_entry = f"probe failed: {error}"
            probe_status = "error"

        elapsed_seconds = max(0.0, time.monotonic() - launch_started)
        running = probe_succeeded and bundle_id in launchctl_entry
        if running:
            if first_seen_at_seconds is None:
                first_seen_at_seconds = math.ceil(elapsed_seconds)
            if elapsed_seconds <= 30:
                seen_running_by_30 = True
                unconfirmed_probe_failure_by_30 = False
        elif probe_succeeded and first_seen_at_seconds is not None:
            if exited_at_seconds is None:
                exited_at_seconds = math.ceil(elapsed_seconds)
            if elapsed_seconds <= 30:
                exited_by_30 = True
        elif not probe_succeeded and first_seen_at_seconds is not None and elapsed_seconds <= 30:
            unconfirmed_probe_failure_by_30 = True

        timestamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
        entry = launchctl_entry.replace("\t", " ")
        timeline.write(
            f"{elapsed_seconds:.3f}\t{timestamp}\t{'yes' if running else 'no'}\t{entry}\t{probe_status}\n"
        )
        timeline.flush()

        scheduled_seconds += 5
        while scheduled_seconds <= elapsed_seconds:
            scheduled_seconds += 5
        if elapsed_seconds >= 60:
            break

for screenshot in screenshots:
    try:
        screenshot.wait(timeout=20)
    except subprocess.TimeoutExpired:
        screenshot.kill()
        screenshot.wait()

print(json.dumps({
    "firstSeenAtSeconds": first_seen_at_seconds,
    "exitedAtSeconds": exited_at_seconds,
    "aliveAfter30Seconds": seen_running_by_30 and not exited_by_30 and not unconfirmed_probe_failure_by_30,
}, separators=(",", ":")))
PY
)
first_seen_at_seconds=$(python3 -c 'import json,sys; print(json.loads(sys.argv[1])["firstSeenAtSeconds"] if json.loads(sys.argv[1])["firstSeenAtSeconds"] is not None else "null")' "$timeline_result")
exited_at_seconds=$(python3 -c 'import json,sys; print(json.loads(sys.argv[1])["exitedAtSeconds"] if json.loads(sys.argv[1])["exitedAtSeconds"] is not None else "null")' "$timeline_result")
if [ "$(python3 -c 'import json,sys; print(str(json.loads(sys.argv[1])["aliveAfter30Seconds"]).lower())' "$timeline_result")" = true ]; then
  alive=true
fi
copy_crash_reports
printf 'run %s: alive30=%s firstSeen=%s exited=%s appCrashReports=%s otherCrashReports=%s\n' \
  "${IOS_SMOKE_RUN_NUMBER:-01}" "$alive" "$first_seen_at_seconds" "$exited_at_seconds" "$crash_count" "$other_crash_count"
if [ "$alive" = true ] && [ "$crash_count" -eq 0 ]; then
  result=0
else
  echo "smoke failed: aliveAfter30s=$alive crashReports=$crash_count" >&2
  echo "last lines of launch.log:" >&2
  tail -n 30 "$out_dir/launch.log" >&2 || true
fi
exit 0
