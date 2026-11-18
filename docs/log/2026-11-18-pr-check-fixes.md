# 2026-11-18: PR check failures

## Done

- Made the Android E2E setup retry `adb root` when the emulator briefly closes its ADB connection during startup. The failing PR run reported `adb: unable to connect for root: closed` immediately after the emulator booted.
- The retry waits for the emulator to reconnect and report `sys.boot_completed=1`; other `adb root` errors still fail immediately.
- Prevented stale chat reloads from overwriting a newer timeline after asynchronous model-catalog requests.
- Made acceptance polling evaluate UI-bound conditions on the Avalonia dispatcher, including calls that begin off-thread, without nesting dispatch when already on the UI thread.
- Added deterministic regression tests for the stale timeline and off-thread polling cases.
- `ImageGen.Presentation.Tests`: 559 passed. `ImageGen.Acceptance.Tests`: 88 total, 86 passed, 2 skipped. Formatting, `git diff --check`, and `bash scripts/check.sh` passed.
- The Android E2E run [44267360973](https://github.com/r-eliseev/studious-goggles/actions/runs/44267360973) passed, including its 4 UI scenarios.

## Decisions and reasons

- Retry only transient transport errors; do not hide unsupported root access or other setup failures.

## Not finished

- The latest full PR build run still needs to validate the acceptance fix; it was queued on the preceding commit when `avalonia` failed.

## Points of attention

- PR #116 `test-android` initially failed in run [44194057784](https://github.com/r-eliseev/studious-goggles/actions/runs/44194057784), at the initial `adb root` call. The emulator had reported boot complete moments earlier. The retry succeeded in run 44267360973.
- The same PR build reported an empty chat after sending a message in scenario “Editing a generation prompt starts a run without the assistant” (run 44267357730). An older reload could resume after its catalog await and clear the newer timeline. A full local acceptance run passed after adding an epoch check.
