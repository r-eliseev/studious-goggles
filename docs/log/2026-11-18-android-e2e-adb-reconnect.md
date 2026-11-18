# 2026-11-18: Android E2E ADB reconnect

## Done

- Made the Android E2E setup retry `adb root` when the emulator briefly closes its ADB connection during startup. The failing PR run reported `adb: unable to connect for root: closed` immediately after the emulator booted.
- The retry waits for the emulator to reconnect and report `sys.boot_completed=1`; other `adb root` errors still fail immediately.
- `bash -n ImageGen/ImageGen.E2E.Common/ci/android-mock-e2e.sh`, `git diff --check`, and `bash scripts/check.sh` passed. CI rerun is pending.

## Decisions and reasons

- Retry only transient transport errors; do not hide unsupported root access or other setup failures.

## Not finished

- The change has not yet been exercised by a fresh GitHub Actions Android E2E run.

## Points of attention

- PR #116 check `test-android` failed in run [44194057784](https://github.com/r-eliseev/studious-goggles/actions/runs/44194057784), at the initial `adb root` call. The emulator had reported boot complete moments earlier.
