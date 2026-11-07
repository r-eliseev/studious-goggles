#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -lt 1 ] || [ "$#" -gt 3 ]; then
  echo "usage: $0 <report-dir> [avalonia-macos.tar.gz] [maui-maccatalyst.tar.gz]" >&2
  exit 2
fi

report_dir=$1
avalonia_artifact=${2:-}
catalyst_artifact=${3:-}
script_dir=$(cd "$(dirname "$0")" && pwd)
repo_root=$(cd "$script_dir/../../.." && pwd)
mkdir -p "$report_dir"
started_epoch=$(date +%s)
touch "$report_dir/run-start.marker"
gui_session=false
accessibility_ok=false
resolution=unknown
non_uniform_ratio=0
app_failures=()

who >"$report_dir/who.txt" 2>&1 || true
uid=$(id -u)
if launchctl print "gui/$uid" >"$report_dir/gui-domain.txt" 2>&1; then
  gui_session=true
fi
head -20 "$report_dir/gui-domain.txt" >"$report_dir/gui-domain-head.txt" || true
system_profiler SPDisplaysDataType >"$report_dir/displays.txt" 2>&1 || true
resolution=$(awk -F': ' '/Resolution:/ { print $2; exit }' "$report_dir/displays.txt")
resolution=${resolution:-unknown}

if screencapture -x "$report_dir/desktop.png" >"$report_dir/screencapture.log" 2>&1; then
  if sips -s format bmp "$report_dir/desktop.png" --out "$report_dir/desktop.bmp" >>"$report_dir/screencapture.log" 2>&1; then
    metrics=$(python3 -I "$script_dir/image-nonuniform.py" "$report_dir/desktop.bmp")
    printf '%s\n' "$metrics" >"$report_dir/desktop-metrics.json"
    non_uniform_ratio=$(python3 -c 'import json,sys; print(json.loads(sys.argv[1])["ratio"])' "$metrics")
  fi
fi

if osascript -e 'tell application "System Events" to get name of every process whose background only is false' >"$report_dir/accessibility.txt" 2>&1; then
  accessibility_ok=true
fi

smoke_app() {
  local label=$1
  local artifact=$2
  local bundle_id=$3
  local app_name=$4
  local app_dir="$repo_root/tmp/apple-macos-$label-$$"
  local app_path executable app_executable_path app_plist start_marker process_ids process_id

  if [ -z "$artifact" ] || [ ! -f "$artifact" ]; then
    printf 'Artifact unavailable (build may be blocked).\n' >"$report_dir/app-$label.txt"
    return 0
  fi

  mkdir -p "$app_dir"
  if ! tar -xzf "$artifact" -C "$app_dir"; then
    printf 'Could not extract app artifact.\n' >"$report_dir/app-$label.txt"
    app_failures+=("$label:extract")
    rm -rf "$app_dir"
    return 0
  fi
  app_path=$(find "$app_dir" -type d -name '*.app' -print -quit)
  if [ -z "$app_path" ]; then
    printf 'Archive did not contain an .app bundle.\n' >"$report_dir/app-$label.txt"
    app_failures+=("$label:missing-app")
    rm -rf "$app_dir"
    return 0
  fi
  app_plist="$app_path/Contents/Info.plist"
  if [ -f "$app_plist" ]; then
    executable=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleExecutable' "$app_plist")
    app_executable_path="$app_path/Contents/MacOS/$executable"
  else
    app_plist="$app_path/Info.plist"
    executable=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleExecutable' "$app_plist")
    app_executable_path="$app_path/$executable"
  fi

  if ! codesign --force --deep --sign - --timestamp=none "$app_path" >"$report_dir/app-$label-codesign.log" 2>&1; then
    printf 'Could not ad-hoc sign app bundle.\n' >"$report_dir/app-$label.txt"
    app_failures+=("$label:codesign")
    rm -rf "$app_dir"
    return 0
  fi
  if ! codesign --verify --deep --strict "$app_path" >>"$report_dir/app-$label-codesign.log" 2>&1; then
    printf 'App bundle code signature verification failed.\n' >"$report_dir/app-$label.txt"
    app_failures+=("$label:codesign-verify")
    rm -rf "$app_dir"
    return 0
  fi

  start_marker="$report_dir/$label-start.marker"
  touch "$start_marker"
  if ! open -n "$app_path" >"$report_dir/app-$label-open.log" 2>&1; then
    app_failures+=("$label:open")
    rm -rf "$app_dir"
    return 0
  fi
  sleep 15
  if pgrep -f "$app_name" >/dev/null; then
    printf 'running=true\n' >"$report_dir/app-$label.txt"
  else
    printf 'running=false\n' >"$report_dir/app-$label.txt"
    app_failures+=("$label:not-running")
  fi
  screencapture -x "$report_dir/app-$label.png" >"$report_dir/app-$label-screenshot.log" 2>&1 || app_failures+=("$label:screenshot")

  mkdir -p "$report_dir/crash"
  while IFS= read -r -d '' report; do
    cp "$report" "$report_dir/crash/"
  done < <(find "$HOME/Library/Logs/DiagnosticReports" -type f \( -name '*.crash' -o -name '*.ips' \) -newer "$start_marker" -print0 2>/dev/null || true)
  osascript -e "tell application id \"$bundle_id\" to quit" >"$report_dir/app-$label-close.log" 2>&1 || true
  process_ids=$(pgrep -f "$app_executable_path" || true)
  for process_id in $process_ids; do
    [ "$process_id" = "$$" ] || kill "$process_id" >/dev/null 2>&1 || true
  done
  sleep 1
  if ! pgrep -f "$app_executable_path" >/dev/null 2>&1; then
    rm -rf "$app_dir"
  fi
}

smoke_app avalonia "$avalonia_artifact" com.eliseev.imagegen.avalonia ImageGen.Avalonia
smoke_app catalyst "$catalyst_artifact" com.eliseev.imagegen ImageGen.Maui

mkdir -p "$report_dir/crash"
crash_count=$(find "$report_dir/crash" -type f | wc -l | tr -d ' ')
python3 - "$report_dir/summary.json" "$gui_session" "$resolution" "$non_uniform_ratio" "$accessibility_ok" "$crash_count" "${app_failures[*]-}" <<'PY'
import json
import sys

summary = {
    "guiSession": sys.argv[2] == "true",
    "resolution": sys.argv[3],
    "nonUniformRatio": float(sys.argv[4]),
    "accessibilityOk": sys.argv[5] == "true",
    "crashReports": int(sys.argv[6]),
    "appFailures": sys.argv[7].split() if sys.argv[7] else [],
}
with open(sys.argv[1], "w", encoding="utf-8") as output:
    json.dump(summary, output, indent=2)
    output.write("\n")
PY

if [ "$gui_session" != true ] || [ "$accessibility_ok" != true ] || ! python3 -c 'import sys; raise SystemExit(0 if float(sys.argv[1]) > 0.01 else 1)' "$non_uniform_ratio" || [ "$crash_count" -gt 0 ] || [ "${#app_failures[@]}" -gt 0 ]; then
  exit 1
fi
