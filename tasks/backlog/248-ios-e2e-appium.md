---
id: 248
status: approved
assignee:
claimed:
created: 2026-11-07
---

# iOS UI tests: Appium XCUITest for MAUI and Avalonia, video and Allure

## Description
Stage P4 of the Apple addendum (ADR 0030, `docs/specs/apple-ci-e2e.md`, requirements 5–6): the NUnit project `ImageGen.E2E.iOS` runs the same scenarios on MAUI iOS and Avalonia iOS in the simulator through Appium XCUITest, with video and evidence according to the ADR 0029 contract.

Depends on: 247 (simulator and `.app` work), 240 (fixture, `TestArtifacts`), 243 (HTTPS mock, for the generation scenario), 239 (`TestIds`, `AutomationIdsTests`).
Environment: GitHub Actions `macos-26`; locally, build the project on Linux (`net10.0`, without iOS dependencies) and run `dotnet format`.

## Acceptance criteria
- Step 1, locators (unknown as of 2026-11-07): add a temporary step to smoke job 247 — Appium 3.9.0, `appium driver install xcuitest@12.22.8`, and a session that saves the first-run screen `PageSource` for each app to `ui-maui.xml` and `ui-avalonia.xml`. Record in `docs/knowledge/apple-ci.md` which attribute (`name`, `label`, `identifier`) contains `apikey.entry` for MAUI and what Avalonia exposes. Avalonia markup currently has only `AutomationProperties.Name` (108), not `AutomationProperties.AutomationId`: add `AutomationProperties.AutomationId` to the same elements as in 239 (screens from `TestIds`, files under `ImageGen/ImageGen.Avalonia/Views/`) and check whether it appears as `identifier` on iOS. If Avalonia iOS does not expose the id in the accessibility tree, record this and locate elements by `name` (= `AutomationProperties.Name`, localized text); mark those tests as dependent on the English UI.
- Extend `AutomationIdsTests` (239) to `ImageGen/ImageGen.Avalonia/Views/*.axaml` (`AutomationProperties.AutomationId="<id>"`).
- Project `ImageGen/ImageGen.E2E.iOS/ImageGen.E2E.iOS.csproj`: `net10.0`, NUnit, `Appium.WebDriver` (same version as in 240), `Allure.NUnit` 2.15.0, reference to `ImageGen.E2E.Common`; MTP as in 240. Add it to `ImageGen/ImageGen.slnx`.
- `IosSession`: `IOSDriver`, `PlatformName=iOS`, `AutomationName=XCUITest`, `udid` from `IOS_UDID`, `app` from `APP_PATH` (`.app`), `bundleId` from `APP_BUNDLE_ID`; `APP_SHELL` = `maui` | `avalonia` (in the report folder name and Allure label `parentSuite` = `ios-<shell>`); `newCommandTimeout` 120; `wdaLaunchTimeout` 240000 ms (the first WebDriverAgent build takes a long time).
- Before each test, reset data by reinstalling (`mobile: removeApp` + `mobile: installApp` + `mobile: launchApp`; verify command names against driver 12.22.8 documentation). For the mock scenario, before launch run `xcrun simctl get_app_container <UDID> <bundle> data`, locate the data folder in the container (MAUI `FileSystem.AppDataDirectory`, Avalonia `IosAppPaths` from 246; record the path), and copy `e2e-providers.json` as `providers.json`; install the mock root certificate once per job with `xcrun simctl keychain <UDID> add-root-cert <ca.pem>`.
- Video: use XCUITest `StartRecordingScreen`; if it does not work on the simulator, start a separate `xcrun simctl io <UDID> recordVideo --codec=h264 recording.mp4` process, stop it with `SIGINT`, and wait 10 seconds. Verify the file is larger than 10 KB and duration is greater than 0 (`ffprobe` if available on the image, otherwise `mdls -name kMDItemDurationSeconds`); otherwise, add a line to `errors.txt`.
- On failure, save `screenshot.png` and `ui.xml`; always save `simulator.log` (`log show` for the test duration), `metadata.json`, and Allure attachments; `appium.log` is a job-wide file. Put each operation in its own `try`.
- `FirstRunTests` scenarios (both apps): `Launch_ShowsFirstRunKeyScreen`; `InvalidKey_ShowsError` (as in 240); `Relaunch_KeepsFirstRunScreen` (`mobile: terminateApp` + `mobile: launchApp` → same screen, app has not crashed). After 243 is merged, add `MockSessionTests.FirstRunWithMock_GeneratesImage` (as in 243) and the addendum's “back/cancel” scenario: open settings (`sessions.settings`) from the sessions screen and return (find the “Назад” button in the tree; if it needs an id, add a constant to `TestIds` and markup and verify it in `AutomationIdsTests`).
- Jobs `e2e-ios-maui` and `e2e-ios-avalonia` (`macos-26`, `timeout-minutes: 60`, sequential tests, `workflow_dispatch` + `schedule`), Node 24, Appium with `--log reports/ios/<shell>/appium.log`, artifacts `ios-e2e-maui` / `ios-e2e-avalonia` for every outcome.
- Do not retry tests. Three green nightly runs are required; then ask the owner in `docs/status.md` whether to enable them on pull requests.

## Analysis
Reference: the owner's addendum is `docs/specs/ui-e2e-original/MAUI_Avalonia_Apple_CI_E2E_Addendum.md`; ADR 0030 and `docs/specs/apple-ci-e2e.md` take precedence if they differ.
ADR 0029/0030, `docs/specs/ui-e2e.md`, `docs/specs/apple-ci-e2e.md`, projects 240 and 243, `ImageGen/ImageGen.Avalonia/Views/` (verify the key screen filename), and the driver documentation at https://appium.github.io/appium-xcuitest-driver/latest/ (capabilities and `mobile:` commands for version 12.22.8).

## Plan
1. Step 1 (locators), ids in Avalonia markup, and extend `AutomationIdsTests`.
2. Add the project and `IosSession`; add a temporary intentionally failing test to verify evidence capture, then remove it.
3. Add scenarios one at a time and run them in CI on both apps.
4. Add mock scenarios after 243.

Files: `ImageGen/ImageGen.E2E.iOS/` (new: csproj, `IosSession.cs`, `IosTestBase.cs`, `FirstRunTests.cs`, `MockSessionTests.cs`), `ImageGen/ImageGen.E2E.Common/TestIds.cs`, `ImageGen/ImageGen.Presentation.Tests/E2E/AutomationIdsTests.cs`, `ImageGen/ImageGen.Avalonia/Views/*.axaml` (`AutomationProperties.AutomationId` only on elements from `TestIds`), `ImageGen/ImageGen.Maui/Pages/*.xaml` (only missing ids), `ImageGen/Directory.Packages.props`, `ImageGen/ImageGen.slnx`, `.github/workflows/apple-ci.yml`, `docs/knowledge/apple-ci.md`, `docs/status.md`.

### Verification
```
dotnet build ImageGen/ImageGen.E2E.iOS/ImageGen.E2E.iOS.csproj
dotnet format ImageGen/ImageGen.E2E.iOS/ImageGen.E2E.iOS.csproj --verify-no-changes
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
bash scripts/check.sh
# GitHub Actions: e2e-ios-maui and e2e-ios-avalonia, artifacts with one folder per test
```

### Execution order (for Luna)
- First read only the files in the “Files” line and the named tests; compare signatures with current `main`.
- Follow the steps in order (ub-mode): failing test → run it and confirm it fails for the described reason → minimal fix → green test.
- Do not change files outside the “Files” line, rename existing members beyond the plan, or fix neighboring findings; those belong to separate tasks.
- If the code differs from the plan (missing method, different signature, test already exists), do not improvise; stop and write a question in “Comments”.
- Run `dotnet` commands one at a time, not in parallel. After each changed project, run `dotnet format <project> --verify-no-changes`.
- In the “Report”, record what changed, commands and results, and differences from the plan.

## Report

## Comments

### 2026-11-07, claude-audit
Created from the Apple addendum: stage P4. Checked 2026-11-07 (npm): xcuitest 13.x requires Appium 4 beta; for Appium 3 use 12.22.8. Avalonia markup has no AutomationId. Plan for Luna.

### 2026-11-07, owner (in chat, recorded by claude-audit)
The owner's “MAUI + Avalonia — Apple CI/E2E” addendum (original: `docs/specs/ui-e2e-original/`). The plan was approved under the earlier instruction “approve all tasks with plans”. The owner explicitly requested the new `apple-ci.yml` workflow as part of this addendum (ADR 0030).
