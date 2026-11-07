#!/usr/bin/env bash
set -euo pipefail

report_dir="reports/apple-preflight"
mkdir -p "$report_dir"

inventory="$report_dir/inventory.txt"
{
  sw_vers
  uname -m
  xcodebuild -version
  xcode-select -p
  ls /Applications | grep Xcode || true
  xcrun simctl list runtimes
  dotnet --info
  dotnet workload list
} >"$inventory" 2>&1 || exit 1

xcrun simctl list -j devices available >"$report_dir/devices.json" || exit 1
xcrun simctl list -j runtimes >"$report_dir/runtimes.json" || exit 1
xcrun simctl list -j devicetypes >"$report_dir/devicetypes.json" || exit 1

xcode_version=$(xcodebuild -version | awk '/^Xcode / && !found { print $2; found=1 }')
sdk_version=$(dotnet --version)
default_log="$report_dir/attempt-default.txt"
default_started=$(date +%s)
default_ios=blocked
default_catalyst=blocked

{
  printf 'Xcode: %s\nSDK: %s\n' "$xcode_version" "$sdk_version"
  run_and_record() {
    local label="$1"
    shift
    printf '\n--- %s ---\n' "$label"
    "$@"
  }

  if run_and_record 'install default maui workload' dotnet workload install maui; then
    if run_and_record 'build MAUI iOS Simulator' dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-ios -c Debug -r iossimulator-arm64 -p:EnableCodeSigning=false; then
      default_ios=ok
    fi
    if run_and_record 'build MAUI Mac Catalyst' dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-maccatalyst -c Debug -r maccatalyst-arm64 -p:EnableCodeSigning=false; then
      default_catalyst=ok
    fi
  fi
} >"$default_log" 2>&1
default_duration=$(( $(date +%s) - default_started ))

# Pick the newest published macios release matching the Xcode actually selected above.
release_json="$report_dir/macios-releases.json"
release_info="$report_dir/pinned-release.txt"
release_prefix="dotnet-10.0.1xx-xcode${xcode_version}-"
release_found=false
workload_set=''
sdk_pinned=''
if curl -fsSL --retry 3 https://api.github.com/repos/dotnet/macios/releases?per_page=100 -o "$release_json"; then
  if python3 - "$release_json" "$release_prefix" "$release_info" <<'PY'
import json
import re
import sys

with open(sys.argv[1], encoding="utf-8") as stream:
    releases = json.load(stream)
prefix = sys.argv[2]
matching = [item for item in releases if not item.get("draft") and item["tag_name"].startswith(prefix)]
for release in matching:
    body = release.get("body", "")
    workload = re.search(r"workload set.*?version\s+[`']?([0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?)", body, re.I | re.S)
    sdk = re.search(r"latest \.NET SDK\s+[`']?([0-9]+\.[0-9]+\.[0-9]+)", body, re.I)
    if workload and sdk:
        with open(sys.argv[3], "w", encoding="utf-8") as output:
            output.write(f"tag={release['tag_name']}\nworkloadSet={workload.group(1)}\ndotnetSdk={sdk.group(1)}\n")
        raise SystemExit(0)
raise SystemExit(1)
PY
  then
    release_found=true
    workload_set=$(awk -F= '$1 == "workloadSet" { print $2 }' "$release_info")
    sdk_pinned=$(awk -F= '$1 == "dotnetSdk" { print $2 }' "$release_info")
  fi
fi

pinned_log="$report_dir/attempt-pinned.txt"
pinned_started=$(date +%s)
pinned_ios=blocked
pinned_catalyst=blocked
if [ "$release_found" = true ]; then
  pinned_dotnet="$HOME/.dotnet-apple-preflight"
  mkdir -p "$pinned_dotnet"
  {
    cat "$release_info"
    printf 'Selected Xcode: %s\n' "$xcode_version"
    if ! curl -fsSL --retry 3 https://dot.net/v1/dotnet-install.sh -o "$report_dir/dotnet-install.sh"; then
      echo 'Could not download dotnet-install.sh'
    elif bash "$report_dir/dotnet-install.sh" --version "$sdk_pinned" --install-dir "$pinned_dotnet"; then
      export DOTNET_ROOT="$pinned_dotnet"
      export PATH="$pinned_dotnet:$PATH"
      if dotnet workload install maui --version "$workload_set"; then
        if dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-ios -c Debug -r iossimulator-arm64 -p:EnableCodeSigning=false; then
          pinned_ios=ok
        fi
        if dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-maccatalyst -c Debug -r maccatalyst-arm64 -p:EnableCodeSigning=false; then
          pinned_catalyst=ok
        fi
      fi
    fi
  } >"$pinned_log" 2>&1
else
  printf 'No published macios release matching %s in https://github.com/dotnet/macios/releases (checked GitHub releases API).\n' "$release_prefix" >"$pinned_log"
fi
pinned_duration=$(( $(date +%s) - pinned_started ))

tail -n 80 "$default_log" >"$report_dir/attempt-default-tail.txt"
tail -n 80 "$pinned_log" >"$report_dir/attempt-pinned-tail.txt"

default_error=$(grep -Eim1 'error |requires Xcode|Xcode .*required|Xcode version|NETSDK1147' "$default_log" || true)
pinned_error=$(grep -Eim1 'error |requires Xcode|Xcode .*required|Xcode version|NETSDK1147' "$pinned_log" || true)
if [ -z "$default_error" ]; then default_error="no compatible build result recorded; see attempt-default-tail.txt"; fi
if [ -z "$pinned_error" ]; then pinned_error="no compatible build result recorded; see attempt-pinned-tail.txt"; fi
default_ios_status="$default_ios"
default_catalyst_status="$default_catalyst"
pinned_ios_status="$pinned_ios"
pinned_catalyst_status="$pinned_catalyst"
if [ "$default_ios" != ok ]; then default_ios_status="blocked: toolchain mismatch ($default_error; Xcode $xcode_version, default workload)"; fi
if [ "$default_catalyst" != ok ]; then default_catalyst_status="blocked: toolchain mismatch ($default_error; Xcode $xcode_version, default workload)"; fi
if [ "$release_found" != true ]; then pinned_error="no published macios release matching $release_prefix"; fi
if [ "$pinned_ios" != ok ]; then pinned_ios_status="blocked: toolchain mismatch ($pinned_error; Xcode $xcode_version, workload $workload_set)"; fi
if [ "$pinned_catalyst" != ok ]; then pinned_catalyst_status="blocked: toolchain mismatch ($pinned_error; Xcode $xcode_version, workload $workload_set)"; fi

python3 - "$report_dir/summary.json" "$xcode_version" "$sdk_version" "$workload_set" "$default_ios_status" "$default_catalyst_status" "$default_duration" "$pinned_ios_status" "$pinned_catalyst_status" "$pinned_duration" "$release_found" <<'PY'
import json
import sys

keys = ("xcode", "dotnetSdk", "workloadSet", "mauiIos", "mauiCatalyst", "defaultSeconds", "pinnedMauiIos", "pinnedMauiCatalyst", "pinnedSeconds", "pinnedReleaseFound")
values = sys.argv[2:]
summary = dict(zip(keys, values))
for key in ("defaultSeconds", "pinnedSeconds"):
    summary[key] = int(summary[key])
summary["pinnedReleaseFound"] = summary["pinnedReleaseFound"] == "true"
with open(sys.argv[1], "w", encoding="utf-8") as output:
    json.dump(summary, output, indent=2)
    output.write("\n")
PY
