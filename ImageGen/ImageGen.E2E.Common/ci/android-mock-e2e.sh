#!/usr/bin/env bash
set -euo pipefail

adb_binary="$(command -v adb)"
adb() {
  timeout --foreground --kill-after=5s 60s "$adb_binary" "$@"
}

mkdir -p reports/android/mock-provider
dotnet ImageGen/ImageGen.E2E.MockProvider/bin/Release/net10.0/ImageGen.E2E.MockProvider.dll --urls https://0.0.0.0:5443 > reports/android/mock-provider/mock-provider.log 2>&1 &
mock_pid=$!
trap 'kill "$mock_pid" 2>/dev/null || true' EXIT

ready=false
for attempt in $(seq 1 120); do
  if curl --silent --fail --cacert "$E2E_CA_CERT" \
    --header 'Authorization: Bearer e2e-mock-key' \
    https://localhost:5443/v1/models > reports/android/mock-provider/health.json; then
    ready=true
    break
  fi
  sleep 1
done
if [[ "$ready" != true ]]; then
  cat reports/android/mock-provider/mock-provider.log
  exit 1
fi

adb root
adb wait-for-device
remount_output="$(adb remount 2>&1)" || { printf '%s\n' "$remount_output"; exit 1; }
printf '%s\n' "$remount_output"
if grep -qi 'reboot' <<< "$remount_output"; then
  adb reboot
  adb wait-for-device
  for attempt in $(seq 1 180); do
    if [[ "$(adb shell getprop sys.boot_completed | tr -d '\r')" == 1 ]]; then break; fi
    sleep 1
  done
  adb root
  adb wait-for-device
  adb remount
fi

cert_hash="$(openssl x509 -subject_hash_old -noout -in "$E2E_CA_CERT" | head -n 1)"
cert_path="/system/etc/security/cacerts/${cert_hash}.0"
adb push "$E2E_CA_CERT" "$cert_path"
adb shell chown 0:0 "$cert_path"
adb shell chmod 644 "$cert_path"
adb shell restorecon "$cert_path"
adb shell ls -lZ "$cert_path"
adb reboot
adb wait-for-device
booted=false
for attempt in $(seq 1 180); do
  boot_state="$(adb shell getprop sys.boot_completed | tr -d '\r' || true)"
  if [[ "$boot_state" == 1 ]]; then
    booted=true
    break
  fi
  sleep 1
done
if [[ "$booted" != true ]]; then
  echo 'Android emulator did not finish booting after installing the test CA.' >&2
  exit 1
fi
adb root
adb wait-for-device
adb reverse tcp:5443 tcp:5443

timeout --foreground --kill-after=5s 120s appium driver doctor uiautomator2
timeout --foreground --kill-after=5s 300s bash ImageGen/ImageGen.E2E.Common/ci/android-preflight.sh
mkdir -p reports/android/allure-results
APP_APK="$(find "$GITHUB_WORKSPACE/publish/android" -maxdepth 1 -type f -name '*-Signed.apk' -print -quit)" \
  timeout --foreground --kill-after=15s 360s dotnet test \
    --project ImageGen/ImageGen.E2E.Android/ImageGen.E2E.Android.csproj \
    --report-trx --results-directory reports/android
