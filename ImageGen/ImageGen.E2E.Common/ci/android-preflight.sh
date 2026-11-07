#!/usr/bin/env bash
set -euo pipefail

report_dir="reports/preflight-android"
mkdir -p "$report_dir"
apk="$(find publish/android -maxdepth 1 -type f -name '*-Signed.apk' -print -quit)"
if [[ -z "$apk" ]]; then
  echo "No signed APK found in publish/android" >&2
  exit 1
fi

root_ok=false
if adb root >"$report_dir/adb-root.txt" 2>&1; then
  root_ok=true
fi
adb wait-for-device
remount_ok=false
if adb remount >"$report_dir/adb-remount.txt" 2>&1; then
  remount_ok=true
fi

adb install -r "$apk"
adb shell monkey -p com.eliseev.imagegen -c android.intent.category.LAUNCHER 1
sleep 20
adb shell uiautomator dump /sdcard/ui.xml
adb pull /sdcard/ui.xml "$report_dir/ui.xml"
adb exec-out screencap -p >"$report_dir/screen.png"
adb shell screenrecord --time-limit 10 /sdcard/r.mp4 || true
adb pull /sdcard/r.mp4 "$report_dir/recording.mp4" || true

pid="$(adb shell pidof -s com.eliseev.imagegen | tr -d '\r' | awk '{print $1}')"
if [[ -n "$pid" ]]; then
  adb logcat -d --pid="$pid" >"$report_dir/logcat.txt"
else
  echo "Application process id was not found." >"$report_dir/logcat.txt"
fi

cat >"$report_dir/summary.json" <<EOF
{
  "adbRootOk": $root_ok,
  "adbRemountOk": $remount_ok,
  "apk": "$(basename "$apk")",
  "automationIdResourceIdLines": $(grep -o 'resource-id="[^"]*apikey.entry[^"]*"' "$report_dir/ui.xml" | sort -u | jq -R . | jq -s . 2>/dev/null || echo '[]'),
  "screenCaptured": $(test -s "$report_dir/screen.png" && echo true || echo false),
  "recordingCaptured": $(test -s "$report_dir/recording.mp4" && echo true || echo false)
}
EOF
