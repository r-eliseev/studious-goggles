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
app_pid=''
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
  python3 - "$out_dir/summary.json" "$runtime_name" "$device_name" "$launched" "$alive" "$crash_count" "$other_crash_count" "$first_seen_at_seconds" "$exited_at_seconds" "$app_pid" "pid" <<'PY'
import json
import sys

keys = ("runtime", "deviceType", "launched", "aliveAfter30s", "crashReports", "otherCrashReports", "firstSeenAtSeconds", "exitedAtSeconds", "pid", "livenessSource")
values = sys.argv[2:]
summary = dict(zip(keys, values))
summary["launched"] = summary["launched"] == "true"
summary["aliveAfter30s"] = summary["aliveAfter30s"] == "true"
summary["crashReports"] = int(summary["crashReports"])
summary["otherCrashReports"] = int(summary["otherCrashReports"])
summary["firstSeenAtSeconds"] = None if summary["firstSeenAtSeconds"] == "null" else int(summary["firstSeenAtSeconds"])
summary["exitedAtSeconds"] = None if summary["exitedAtSeconds"] == "null" else int(summary["exitedAtSeconds"])
summary["pid"] = int(summary["pid"]) if summary["pid"].isdigit() else None
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
xcrun simctl launch "$udid" "$bundle_id" >"$out_dir/launch.log" 2>&1 &
launch_pid=$!
timeline_result=$(python3 - "$udid" "$bundle_id" "$out_dir" "$launch_started_monotonic" "$out_dir/launch.log" <<'PY'
import datetime
import json
import math
import pathlib
import re
import subprocess
import sys
import time

udid, bundle_id, out_dir, launch_started, launch_log = sys.argv[1:]
out_dir = pathlib.Path(out_dir)
launch_log = pathlib.Path(launch_log)
launch_started = float(launch_started)
pid_pattern = re.compile(r"^" + re.escape(bundle_id) + r":\s+(\d+)\s*$")
app_pid = None
first_seen_at_seconds = None
exited_at_seconds = None
alive_at_30_seconds = False
screenshots = []

def read_app_pid():
    try:
        with launch_log.open(encoding="utf-8", errors="replace") as launch_output:
            for line in launch_output:
                match = pid_pattern.match(line.strip())
                if match:
                    return int(match.group(1))
    except OSError:
        pass
    return None

def check_pid(pid):
    try:
        probe = subprocess.run(
            ["ps", "-p", str(pid), "-o", "pid=,stat="],
            check=False,
            capture_output=True,
            text=True,
            timeout=2,
        )
    except (OSError, subprocess.TimeoutExpired):
        return None

    fields = probe.stdout.strip().split()
    if probe.returncode != 0 or not fields or fields[0] != str(pid):
        return False
    return len(fields) < 2 or not fields[1].startswith("Z")

with (out_dir / "process-timeline.txt").open("w", encoding="utf-8") as timeline:
    timeline.write("elapsedSeconds\ttimestampUtc\trunning\tpid\tpidStatus\tlaunchctlEntry\tprobeStatus\n")
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

        elapsed_seconds = max(0.0, time.monotonic() - launch_started)
        timestamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
        observed_pid = read_app_pid()
        if observed_pid is not None:
            app_pid = observed_pid
        pid_alive = check_pid(app_pid) if app_pid is not None else None
        running = pid_alive is True
        if app_pid is None:
            pid_status = "not-launched"
        elif pid_alive is True:
            pid_status = "alive"
        elif pid_alive is False:
            pid_status = "not-running"
        else:
            pid_status = "probe-error"

        if running:
            if first_seen_at_seconds is None:
                first_seen_at_seconds = math.ceil(elapsed_seconds)
        elif pid_alive is False and first_seen_at_seconds is not None:
            if exited_at_seconds is None:
                exited_at_seconds = math.ceil(elapsed_seconds)
        if scheduled_seconds == 30:
            alive_at_30_seconds = running

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

        entry = launchctl_entry.replace("\t", " ")
        timeline.write(
            f"{elapsed_seconds:.3f}\t{timestamp}\t{'yes' if running else 'no'}\t{app_pid or ''}\t{pid_status}\t{entry}\t{probe_status}\n"
        )
        timeline.flush()

        elapsed_after_diagnostics = max(0.0, time.monotonic() - launch_started)
        scheduled_seconds += 5
        while scheduled_seconds <= elapsed_after_diagnostics:
            if scheduled_seconds == 30:
                break
            scheduled_seconds += 5
        if elapsed_after_diagnostics >= 60:
            break

for screenshot in screenshots:
    try:
        screenshot.wait(timeout=20)
    except subprocess.TimeoutExpired:
        screenshot.kill()
        screenshot.wait()

print(json.dumps({
    "pid": app_pid,
    "firstSeenAtSeconds": first_seen_at_seconds,
    "exitedAtSeconds": exited_at_seconds,
    "aliveAfter30Seconds": alive_at_30_seconds,
}, separators=(",", ":")))
PY
)
app_pid=$(python3 -c 'import json,sys; value=json.loads(sys.argv[1])["pid"]; print(value if value is not None else "")' "$timeline_result")
if [ -n "$app_pid" ]; then
  launched=true
fi
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
