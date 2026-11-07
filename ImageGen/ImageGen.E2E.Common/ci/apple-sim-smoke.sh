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
launched=false
alive=false
crash_count=0
launch_pid=''
result=1

copy_crash_reports() {
  mkdir -p "$out_dir/crash"
  local sources=("$HOME/Library/Logs/DiagnosticReports" "$HOME/Library/Developer/CoreSimulator/Devices/$udid/data/Library/Logs/CrashReporter")
  local source
  for source in "${sources[@]}"; do
    [ -d "$source" ] || continue
    while IFS= read -r -d '' report; do
      cp "$report" "$out_dir/crash/"
    done < <(find "$source" -type f \( -name '*.crash' -o -name '*.ips' \) -newer "$out_dir/run-start.marker" -print0 2>/dev/null || true)
  done
  crash_count=$(find "$out_dir/crash" -type f | wc -l | tr -d ' ')
}

write_summary() {
  python3 - "$out_dir/summary.json" "$runtime_name" "$device_name" "$launched" "$alive" "$crash_count" <<'PY'
import json
import sys

keys = ("runtime", "deviceType", "launched", "aliveAfter15s", "crashReports")
values = sys.argv[2:]
summary = dict(zip(keys, values))
summary["launched"] = summary["launched"] == "true"
summary["aliveAfter15s"] = summary["aliveAfter15s"] == "true"
summary["crashReports"] = int(summary["crashReports"])
with open(sys.argv[1], "w", encoding="utf-8") as output:
    json.dump(summary, output, indent=2)
    output.write("\n")
PY
}

cleanup() {
  local exit_code=$?
  set +e
  if [ -n "$launch_pid" ]; then
    kill "$launch_pid" >/dev/null 2>&1
    wait "$launch_pid" >/dev/null 2>&1
  fi
  if [ -n "$udid" ]; then
    if command -v gtimeout >/dev/null 2>&1; then
      gtimeout 60 xcrun simctl spawn "$udid" log show --last 5m --predicate 'process CONTAINS "ImageGen" OR process CONTAINS "Omnilite"' >"$out_dir/simulator.log" 2>&1 || echo "simulator log collection timed out or failed" >>"$out_dir/simulator.log"
    else
      xcrun simctl spawn "$udid" log show --last 5m --predicate 'process CONTAINS "ImageGen" OR process CONTAINS "Omnilite"' >"$out_dir/simulator.log" 2>&1 || echo "simulator log collection failed" >>"$out_dir/simulator.log"
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
xcrun simctl launch --console-pty "$udid" "$bundle_id" >"$out_dir/launch.log" 2>&1 &
launch_pid=$!
launched=true
sleep 15
if xcrun simctl spawn "$udid" launchctl list | grep -Fq "$bundle_id"; then
  alive=true
fi
xcrun simctl io "$udid" screenshot "$out_dir/screenshot.png"
sleep 15
xcrun simctl io "$udid" screenshot "$out_dir/screenshot-30s.png"
copy_crash_reports
if [ "$alive" = true ] && [ "$crash_count" -eq 0 ]; then
  result=0
else
  echo "smoke failed: aliveAfter15s=$alive crashReports=$crash_count" >&2
fi
exit 0
