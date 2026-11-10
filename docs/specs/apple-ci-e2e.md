# Apple CI and UI tests: MAUI and Avalonia on iOS and macOS

Status: draft
Code: `.github/workflows/apple-ci.yml`, `ImageGen/ImageGen.Avalonia.iOS`, `ImageGen/ImageGen.E2E.iOS`, `ImageGen/ImageGen.E2E.Common/ci/apple-*` — appear in tasks 244–250.

The basis is the owner's addendum "MAUI + Avalonia — Apple CI/E2E" of 2026-11-07. The original is in `docs/specs/ui-e2e-original/`. It is an addendum to `docs/specs/ui-e2e.md`, not its replacement. Decisions and verified facts are in ADR 0030. iOS UI tests are written as Gherkin scenarios (tag `@e2e`) like the other E2E tests (ADR 0031, task 266).

## Goal
On GitHub runners, without an Apple ID, certificates and secrets, get:
- separate statuses of four builds — MAUI iOS, MAUI Mac Catalyst, Avalonia iOS, Avalonia macOS;
- a launch of both iOS apps in the Simulator with evidence;
- iOS UI tests;
- a GUI check on macOS.

None of this is published as a release.

## Requirements
1. **Tool incompatibility is shown explicitly.**
   - The `apple-ci.yml` workflow needs no secrets and does not publish releases. `release.yml` does not change.
   - An Xcode/workload incompatibility is printed explicitly: `blocked: toolchain mismatch` with the versions.
   - A job with such an incompatibility fails but does not block the PR. It is never green.
2. **The build matrix.**

   | App | Target | Command (refined by 244/245) | Bundle id |
   |---|---|---|---|
   | MAUI | iOS Simulator | `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-ios -c Debug -r iossimulator-arm64 -p:EnableCodeSigning=false` | `com.eliseev.imagegen` |
   | MAUI | Mac Catalyst | `… -f net10.0-maccatalyst -c Debug -r maccatalyst-arm64 -p:EnableCodeSigning=false` | `com.eliseev.imagegen` |
   | Avalonia | iOS Simulator | `dotnet build ImageGen/ImageGen.Avalonia.iOS/ImageGen.Avalonia.iOS.csproj -c Debug -r iossimulator-arm64` | `com.eliseev.imagegen.avalonia` |
   | Avalonia | macOS | `dotnet publish ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj -c Release -r osx-arm64 --self-contained`, then packaging into a `.app` | `com.eliseev.imagegen.avalonia` |

   Separate x64 jobs are not needed. If they are needed, they are added without mixing with arm64.
3. **Artifacts.**
   - `.app` bundles are packed into `tar.gz` so that permissions are kept.
   - Artifacts are labeled "CI only", uploaded on any outcome and kept for 14 days.
   - Unsigned `.app` bundles are not suitable for devices or distribution.
4. **Launch in the iOS Simulator.**
   - A new iPhone is created from the available devices and the iOS runtime. The UDID is found dynamically.
   - Steps: `boot`, waiting for `bootstatus` (with a timeout), install, launch. Then it is checked that the process is alive after 10 s and that there is no crash report.
   - A screenshot, the launch log, the simulator system log and crash reports are saved.
   - The simulator is deleted in `always()`.
5. **iOS UI tests.**
   - Stack: NUnit + Allure + Appium 3.9.0 + `xcuitest@12.22.8` (after 266: Reqnroll on NUnit with `Allure.Reqnroll`).
   - Tests run sequentially on one simulator; data is isolated by reinstalling.
   - Retries do not hide instability: there are no repeated runs.
   - The test folder: `reports/ios/<maui|avalonia>/<Class>.<Test>/`. It contains `recording.mp4`, on failure `screenshot.png` and `ui.xml`, and also `appium.log`, `simulator.log` and `metadata.json`.
6. **Scenarios.**
   - A smoke for each iOS app: launch and the first-launch screen; input and navigation; going back or cancelling; a restart without losing state.
   - Generation — only through the HTTPS mock of task 243. Real paid APIs are forbidden.
7. **macOS.** First a GUI check: a session, a meaningful screenshot, Accessibility permissions. If the check fails, it is an infrastructure error with evidence, not a green test.
8. **Secrets.** Screenshots, videos and logs contain no real keys. Only the fake keys from ADR 0029 are used.

## Acceptance criteria
- Windows and Android from ADR 0029 work without regression.
- Actions has no certificates, profiles or new mandatory Apple secrets.
- Each of the four builds has its own status. An unfinished Avalonia iOS head is not passed off as a success.
- An Xcode/.NET incompatibility gives an explicit `blocked` with the versions.
- Both iOS apps install and open in the Simulator. Evidence is kept also when the job fails.
- iOS UI tests give NUnit, TRX, Allure, video, the UI tree and screenshots.
- macOS UI tests do not count as successful until the GUI check passes.
- No IPA or DMG is published.

## Constraints and out of scope
- Signing, notarization and installing on an iPhone are out of scope.
- Avalonia Android E2E is not here, see ADR 0029.
- The addendum's requirement "unit-tests and Avalonia Headless on PR" is already met: the `core` and `avalonia` jobs in `build.yml`.

## Tests
- The E2E projects themselves are run by CI.
- The `.app` packaging scripts and the `simctl list -j` parser get shell checks on sample files in `tmp/` (tasks 244, 245).
