# UI tests of the built app (E2E) in GitHub Actions

Status: draft (base scenarios 238–242 implemented; mock scenario 243 in progress; migration to Gherkin — task 266)
Code: `ImageGen/ImageGen.E2E.Common`, `ImageGen/ImageGen.E2E.Android`, `ImageGen/ImageGen.E2E.Windows`, `.github/workflows/ui-e2e.yml` (tasks 238–242)

The basis is the owner's specification "Automating UI tests of .NET MAUI in GitHub Actions", version 1.0, sent on 2026-11-07 (the original is in `docs/specs/ui-e2e-original/`, delete it when archiving 238–250; Apple platforms are the addendum `docs/specs/apple-ci-e2e.md`, ADR 0030). Below are its requirements adapted to the project, with corrections after checking. Decisions and verified facts are in ADR 0029. Since 2026-11-09 the E2E tests are written as Gherkin scenarios of the catalog `docs/specs/acceptance-scenarios.md` (tag `@e2e`) on Reqnroll (ADR 0031, task 266); the requirements below about drivers, videos and evidence do not change.

## Goal
After the MAUI app is built, CI itself checks the UI on Android and Windows and keeps evidence for every test. A developer opens the latest run in GitHub Actions, sees the passed and failed scenarios and opens the video of the needed test.

## Requirements
1. Only GitHub runners: `ubuntu-latest` and `windows-latest`. No runner of our own.
2. The built apps are tested:
   - Android: the Release APK `com.eliseev.imagegen` built on `windows-latest`. On Ubuntu the `maui-android` workload does not fit on the disk.
   - Windows: an unpacked build `-p:WindowsPackageType=None` and launching `ImageGen.Maui.exe`. MSIX is not used, because the project builds unpackaged.
3. Elements are found only by `AutomationId` with permanent meaningful names `<screen>.<element>`, for example `apikey.entry`, `apikey.save`, `sessions.new`. The names are not localized or generated. The constants are in `ImageGen.E2E.Common/TestIds.cs`.
   - Android: `MobileBy.Id(id)`. `AccessibilityId` is taken by `SemanticProperties.Description`.
   - Windows: UIA `AutomationId` (FlaUI `ByAutomationId`).
   - For list rows the id is `<screen>.item.<stable key>` if there is a key. If there is no key, search by order inside a container with its own id.
4. A separate MP4 video for each test.
   - Android: Appium `StartRecordingScreen` / `StopRecordingScreen`, `timeLimit` 1800 s.
   - Windows: FFmpeg `gdigrab`, 15 fps, H.264, `-movflags +frag_keyframe+empty_moov`. Stopping is `q` on stdin, 10 s to finish, then forced.
5. On failure a screenshot and the UI tree are saved (Android — `PageSource`, Windows — a dump of the UIA tree). Logs are always saved:
   - Android: logcat by the app's PID and `appium.log`;
   - Windows: the FFmpeg log and the app log from `<data folder>/logs`.
   A failure to save one file does not prevent saving the others: each operation is in its own `try`.
6. The test folder: `reports/<android|windows>/<Class>.<Test>/` with the files `recording.mp4`, `screenshot.png`, `ui.xml`, `logcat.txt` or `app.log`, `metadata.json` (name, status, duration, app version, commit).
7. The report is Allure HTML: the results and attachments of both platforms in one `allure-results` folder. A missing platform does not fail the report build.
8. Separate `test-android` and `test-windows` checks in a PR. While the Windows path is experimental, its job does not block (`continue-on-error: true`).
9. Artifacts are uploaded on any outcome and kept for 14 days.
10. No secrets in code and workflows. Only fake keys in tests (`sk-or-e2e-invalid`, the mock's key `e2e-mock-key`). Real provider keys are never passed to E2E.
11. State isolation. Before each test the app's data is clean:
    - Android: Appium `fullReset=false`, `noReset=false` — no reinstall needed, the data is cleared by `pm clear` through `mobile: clearApp`;
    - Windows: the MAUI data folder is deleted before launch. Task 238 records the path.

## Scenarios
- **Smoke without network access to providers (240, 241; Gherkin: KEY-01, KEY-03, KEY-11):**
  1. Launch → the first-launch screen is visible (`apikey.entry`, `apikey.save`).
  2. An empty key → "Save" is unavailable or shows an error — per the current behavior recorded by task 239.
  3. The invalid key `sk-or-e2e-invalid` → the "invalid key" or "no network" error text. This sends a request to OpenRouter without a key: it is free. If the network is unavailable, the test accepts both texts.
  4. Rotating the screen (Android) does not crash the app.
- **With the provider mock (243; Gherkin: CHAT-02, GEN-05):** first launch with the mock provider → the session list → a new session → a message → the assistant's answer and a picture card → fullscreen viewing → deleting the session.

## Acceptance criteria
- The APK launches in the emulator, the test taps a button and checks the result.
- The Windows preflight proves:
  - UI Automation sees the app window;
  - the captured frame is not uniform: the share of pixels different from the mean is more than 1 %.
  If it does not prove it, the fact is recorded and the job stays experimental.
- Every test has its own MP4 that opens.
- On failure there is a screenshot and the UI tree.
- There are Appium and logcat logs, as well as FFmpeg and app logs.
- The Allure HTML contains the results and attachments of both platforms.
- Artifacts are uploaded also when tests fail.
- A PR shows separate Android and Windows statuses.

## Constraints and out of scope
- iOS and Mac Catalyst are out of scope here (ADR 0030 covers them).
- Avalonia Android with the same project — later, as a separate task.
- Live providers are not used in E2E.
- The separate `E2E` build configuration from the original specification is not introduced. The ordinary Release is tested, and the mock is connected through `providers.json`, which is supported anyway. The mock works over HTTPS with a one-off root certificate installed in the emulator's system store (API 33, `-writable-system`, `adb root`). The mock config is put into the data folder through `adb root`. The build does not change. The key is entered through the first-launch screen, because `SecureStorage` cannot be pre-filled. Details: task 243.

## Tests
- The E2E projects themselves (NUnit, after 266 Reqnroll on NUnit) — run by CI.
- `ImageGen.Presentation.Tests/E2E/AutomationIdsTests.cs` checks that all ids from `TestIds` exist in the MAUI markup (task 239). This test runs on Linux without an emulator.
