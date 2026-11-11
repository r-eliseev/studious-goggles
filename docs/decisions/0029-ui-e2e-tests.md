# 0029. UI tests of the built MAUI app in GitHub Actions

Status: accepted
Date: 2026-11-07

Refined by ADR [0031](0031-bdd-acceptance-reqnroll.md) (2026-11-09): the E2E tests are rewritten as Gherkin scenarios on Reqnroll over the same drivers (task 266).

## Context
The owner sent the specification "Automating UI tests of .NET MAUI in GitHub Actions" (NUnit, FlaUI, Appium, Allure, a video of every test) and asked to re-check and plan it. Now the UI is checked like this:
- page models — in `ImageGen.Presentation.Tests`;
- Avalonia — without a screen, in `ImageGen.Avalonia.Tests`;
- MAUI — only by hand (tasks 140 and 046).

Constraints:
- Only GitHub runners (`windows-latest` = Windows Server 2025, `ubuntu-latest`).
- The `maui-android` workload does not install on `ubuntu-latest`: "Disk quota exceeded" (a comment in `.github/workflows/build.yml`). So the APK is built on Windows.
- There are no provider keys in CI (`docs/conventions.md`). Without a key the app shows only the first-launch screen, because `SessionsPageModel.OnAppearingAsync` goes to `ApiKeyPage`, and the key is checked over the network.

Facts checked on 2026-11-07:
- npm: the latest `appium` is 3.9.0. Version 4 exists only as `4.0.0-beta.7`.
- `appium-uiautomator2-driver` 9.x requires `appium ^4.0.0-beta.6`. For Appium 3 the latest is 8.7.3. The `npm install -g appium@4` command from the specification will not work.
- The MAUI documentation "UI testing with Appium" (net-maui-10.0): on Android `AutomationId` is found through `MobileBy.Id` (resource-id), on Windows through `AccessibilityId`. On Android `content-desc` comes from `SemanticProperties.Description`, which the project sets on all elements. So `AppiumBy.AccessibilityId("…")` from the specification finds the wrong thing on Android.
- The project's MAUI markup has no `AutomationId` anywhere (0 occurrences). `SemanticProperties.Description` occurs 103 times.
- Allure: `Allure.NUnit` 2.15.0 is stable. For xUnit v3 there is only `Allure.Xunit.v3` 3.0.0-preview.1, and it needs `xunit.v3.mtp-v2`.
- The Windows 2025 image has WinAppDriver 1.2.2009.02003 and the Android SDK. FFmpeg is not in the image's software list. The screen resolution and whether there is an interactive session are not stated in the image description — unknown.
- FlaUI.UIA3 5.0.0 (2025-02-25) is built for `net8.0-windows`, `net6.0-windows` and `net48`.

## Decision
1. **Tests of the built apps** are separate projects `ImageGen/ImageGen.E2E.Android` and `ImageGen/ImageGen.E2E.Windows` and shared code in `ImageGen/ImageGen.E2E.Common`. So the projects live next to the others in `ImageGen/`, not in `tests/` as in the specification.
2. **The E2E framework is NUnit with `Allure.NUnit`** (the owner's decision of 2026-11-07). This is an exception from ADR 0004 only for the E2E projects. The other tests stay on xUnit v3.
3. **Android is the main path.**
   - Build: a Release APK on `windows-latest`, as in `release.yml`.
   - Tests: on `ubuntu-latest` with the `reactivecircus/android-emulator-runner@v2` emulator (API 33, `google_apis`, `-writable-system`: on API 34+ system certificates live in APEX, and the HTTPS mock needs its own root certificate — task 243), KVM, Appium 3.9.0 and `uiautomator2@8.7.3`. Versions are pinned.
   - Elements are found by `AutomationId` through `MobileBy.Id`. Which attribute of the UI tree carries it is checked in task 238.
4. **Windows is the experimental path.**
   - Driver: FlaUI UIA3, without Appium and WinAppDriver.
   - Video: FFmpeg `gdigrab`, installed by a workflow step.
   - The job's task is a preflight check: is there a session, does UI Automation answer, is the frame not black.
   - Until stability is proven, the job is marked `continue-on-error: true`.
5. **Scenarios.**
   - First smoke without keys: the first-launch screen, an invalid key and the error, element accessibility.
   - Then a local mock of an OpenAI-compatible provider over HTTPS with a one-off test root certificate, connected through `providers.json` (task 243; HTTPS is the owner's decision of 2026-11-07). This is the real code path without keys and without money.
   - Live OpenRouter is not used in CI.
   - For task 243, the emulator gets `providers.json` in its app-private data after each app reset. The mock uses HTTPS with an ephemeral root CA installed in the API 33 emulator system trust store; `adb reverse` exposes the runner's localhost service. This keeps the normal Release app configuration unchanged and does not enable cleartext traffic.
6. **Evidence for every test** in the `reports/<platform>/<test>/` folder:
   - always: a video;
   - on failure: a screenshot and the UI tree;
   - logs: logcat by the app's PID, the Appium log, the FFmpeg log and the app's log.
   All of this is attached to Allure. Artifacts are uploaded on any outcome (`if: always()`).
7. **Workflow** — a new `.github/workflows/ui-e2e.yml`, separate from `build.yml`.
   - Triggers: `workflow_dispatch`, `pull_request`, `push` to `main`.
   - Jobs: `build-android`, `build-windows`, `test-android`, `test-windows`, `report`. The test jobs are separate checks in a PR.
   - The owner explicitly asked for this workflow (the prohibition on changing `.github/workflows/` without a request is lifted for it).
- Checked by task 238: the Windows runner has an active console session and UI Automation sees the window/`apikey.entry`; see `docs/knowledge/ui-e2e.md`. Checked by task 238: the Android AutomationId is found as `resource-id="com.eliseev.imagegen:id/apikey.entry"`; `adb root`/`remount`, screenshot and screenrecord succeed. Details are in `docs/knowledge/ui-e2e.md`.

## Alternatives
- **The Appium Windows driver instead of FlaUI.** Needs WinAppDriver (the image has it, but the MAUI documentation requires exactly 1.2.1) and one more process. FlaUI is simpler and works directly through UIA3.
- **xUnit v3 and the Allure preview.** One framework for the whole project, but the adapter is a preview. The owner chose NUnit.
- **xUnit v3 without Allure** (TRX and a summary). Then there is no HTML report with videos, and the specification requires it.
- **Building the APK on Ubuntu.** Fails on the disk quota.
- **Live OpenRouter.** Money on every run and a dependency on the network and the provider.
- **Avalonia Android with the same tests.** Postponed. The Android test project is not tied to MAUI and can be pointed at the second APK by a separate task after 240.

## Consequences
- The repository gets a second test framework. `docs/conventions.md` must say that NUnit is only for E2E.
- The MAUI markup gets `AutomationId`s with permanent meaningful names (task 239). They are not localized.
- CI gets longer: by how much is unknown, task 238 measures it.
- A video may show the key being typed. Tests use only fake keys (`sk-or-e2e-invalid`, the mock's key). Real keys never reach E2E.
