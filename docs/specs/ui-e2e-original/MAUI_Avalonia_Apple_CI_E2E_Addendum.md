> The owner's addendum to the UI test specification (2026-11-07), translated to English on 2026-11-09 without changes in meaning (the unedited original is the `.docx` next to it), for reference for the executor of tasks 244–250. The corrections from ADR 0030 and `docs/specs/apple-ci-e2e.md` apply: the `unit-tests` and `avalonia-headless` jobs already exist in `build.yml` (`core`, `avalonia`); GitHub runners now have Xcode up to 26.6, while the .NET 10 iOS/Mac Catalyst workload requires Xcode 27, so the iOS/Catalyst builds start with a compatibility check; for Appium 3 — `xcuitest@12.22.8` and `mac2@4.6.1`. Delete the `docs/specs/ui-e2e-original/` folder when archiving tasks 238–250.

To the document "UI E2E testing of .NET MAUI — Windows + Android ·
GitHub-hosted runners · Video, logs and Allure" (07.11.2026).

Repository: https://github.com/r-eliseev/studious-goggles \| Status:
a specification for implementation; does not mean the workflows are already in place.

# A1. Scope and the additivity principle

This is a separate addendum. The original sections 1–12, the existing
Windows/Android jobs, their artifacts, NUnit, Allure, the video/log contract and
the security requirements stay in force. The new Apple jobs do not replace or
change the logic of the existing releases. On a contradiction about
Apple platforms apply this addendum; about Windows/Android — the original
specification.

The goal of the first stage: without an Apple ID, the Apple Developer Program, certificates,
provisioning profiles and private GitHub Secrets, get four
compile checks, logic tests, headless UI and a smoke launch in the
simulator where possible. Do not consider unsigned artifacts
suitable for installing on physical iPhones or for public distribution.

# A2. Platform matrix

| Head     | Target | Runtime / CI format                     | Specifics                                                           |
|----------|--------|-----------------------------------------|---------------------------------------------------------------------|
| MAUI     | iOS    | net10.0-ios / iossimulator-arm64        | A simulator .app without distribution signing                       |
| MAUI     | macOS  | net10.0-maccatalyst / maccatalyst-arm64 | Mac Catalyst, not native AppKit                                     |
| Avalonia | iOS    | Avalonia iOS host / iossimulator-arm64  | Add a separate iOS host, do not reuse the Desktop entrypoint        |
| Avalonia | macOS  | Avalonia desktop / osx-arm64            | A self-contained .app, without a Developer ID                       |

Determine the actual TFMs, project names, bundle IDs and paths from the csproj
at implementation time; the table is the target configuration. If
x64 for macOS is needed, add separate jobs, do not mix with Apple Silicon.

# A3. The new GitHub Actions architecture

Add an independent workflow .github/workflows/apple-ci.yml with the events
pull_request, push (main) and workflow_dispatch; permissions: contents:
read. It must not publish a GitHub Release and must not require
GitHub Secrets. Run the resource-heavy E2E jobs by default on
workflow_dispatch / a schedule, and on a PR only after proven
stability.

| Job                       | Runner        | What it checks                                     | Status at the start                      |
|---------------------------|---------------|----------------------------------------------------|------------------------------------------|
| apple-preflight           | macOS hosted  | Xcode, xcrun, SDK, .NET, iOS runtime, capabilities | PR required after stabilization          |
| unit-tests                | ubuntu-latest | Shared services, ViewModels, without UI            | PR required                              |
| avalonia-headless         | ubuntu-latest | Controls, bindings, navigation, input              | PR required                              |
| build-maui-ios            | macOS hosted  | Unsigned simulator .app                            | PR required after the preflight          |
| build-maui-catalyst       | macOS hosted  | Unsigned Catalyst .app                             | PR required after the preflight          |
| build-avalonia-ios        | macOS hosted  | iOS host + simulator .app                          | PR required after the host is created    |
| build-avalonia-macos      | macOS hosted  | Desktop publish + .app packaging                   | PR required                              |
| smoke-ios-{maui,avalonia} | macOS hosted  | Boot → install → launch → evidence                 | Nightly/manual → PR                      |
| e2e-ios-{maui,avalonia}   | macOS hosted  | Appium XCUITest + NUnit + evidence                 | Manual/nightly                           |
| smoke-macos / e2e-macos   | macOS hosted  | Launching the .app; then platform UI automation    | Preflight separately; initially non-gating |
| apple-report              | ubuntu-latest | Allure HTML + aggregate                            | always()                                 |

# A4. Preflight: mandatory fail-fast

- Print sw_vers, uname -m, xcodebuild -version, xcode-select -p, xcrun
  simctl list -j devices available, xcrun simctl list runtimes, dotnet
  --info, dotnet workload list, the list of available SDKs.

- Match the version of the installed .NET workload against Xcode.
  Record an incompatibility explicitly: "blocked: toolchain mismatch", and do not
  mask it as a failure of the app code.

- Make sure an iPhone simulator is available on the chosen architecture;
  find the device UDID dynamically, do not hardcode the model/iOS version.

- Set a timeout for bootstatus and for each test; stop a hung
  simulator job with clear diagnostics.

- For macOS window automation run a separate preflight: a GUI
  session, Accessibility permissions, the ability to find a window/get a
  meaningful screenshot. On failure do not mark UI tests
  successful.

Example diagnostic commands (not a full workflow):

xcodebuild -version  
xcrun simctl list devices available  
xcrun simctl list runtimes  
dotnet --info  
dotnet workload list

# A5. Compiling without signing

The build job must use the SDK matching the repository (global.json
currently sets the test runner settings, so pin the specific
SDK version separately/in the workflow). The commands are an example; check the right
project paths, TFMs and properties against the solution files and the available
.NET SDK.

dotnet build \<maui.csproj\> -f net10.0-ios -c Debug \\  
-r iossimulator-arm64 -p:EnableCodeSigning=false  
  
dotnet build \<maui.csproj\> -f net10.0-maccatalyst -c Debug \\  
-r maccatalyst-arm64 -p:EnableCodeSigning=false  
  
dotnet publish \<avalonia-desktop.csproj\> -c Release \\  
-r osx-arm64 --self-contained true

For Avalonia iOS create a host project from the platform template,
connect the shared Avalonia Application and use a separate simulator
RID. Do not move Avalonia.Desktop/the classic desktop
lifetime into it. Determine the build command after the host is created.

Do not mix an unsigned simulator .app with a device-ready IPA. For MAUI Mac
Catalyst check that the artifact really launches on hosted
macOS; launching the app in CI may be limited by the GUI session.

# A6. iOS Simulator smoke: two independent apps

1.  Create/choose an iPhone Simulator from the available ones, run xcrun
    simctl boot and wait for bootstatus with a timeout.

2.  Download the corresponding simulator .app as an artifact, check its
    structure, CFBundleIdentifier and target architecture.

3.  Run xcrun simctl install \<UDID\> \<App.app\> and xcrun simctl
    launch \<UDID\> \<BundleId\>.

4.  Check the process and the crash report; save a screenshot,
    simctl/launch logs, syslog/crash diagnostics. One successful launch
    by itself does not prove that the UI works.

5.  Shutdown/delete the temporary simulator in always(); keep the evidence
    even on error.

xcrun simctl boot "\$UDID"  
xcrun simctl bootstatus "\$UDID" -b  
xcrun simctl install "\$UDID" "path/to/App.app"  
xcrun simctl launch "\$UDID" "com.example.app"  
xcrun simctl io "\$UDID" screenshot reports/ios/smoke.png

# A7. UI E2E and diagnostics: extending the original contract

Keep the NUnit + Appium + Allure stack from the original sections 5–8.
Add the iOS Appium XCUITest driver. For MAUI and Avalonia apply shared
scenarios/test ID contracts, but determine the platform locators from the
real accessibility tree (AutomationId ≠ a guaranteed
accessibility id).

- Minimal scenarios for each iOS app: boot+launch, the main
  screen is available, input/navigation, back/cancel, recovery after
  a restart. Network generation is not a mandatory part of the smoke;
  for E2E use a fixture/mock backend without paid APIs.

- Each test: a separate folder reports/ios/\<shell\>/\<Test.ID\>;
  recording.mp4 (if screen recording is supported), screenshot.png
  on error, ui.xml/page-source, appium.log, simulator.log, TRX, Allure
  attachments.

- For iOS video recording use a supported Appium/XCUITest
  mechanism or xcrun simctl io recordVideo with a proper stop;
  normalize the .mp4/.mov differences if needed. Check that
  the video is not empty and plays.

- Do not run several E2E at once with one simulator:
  sequential execution and separate UDIDs / data isolation. Retrying
  tests must not hide a flaky result.

- macOS UI E2E: first check the availability of GUI/Accessibility on
  the hosted runner, then choose Appium Mac2 / a suitable tool. If the
  environment is unavailable — an infrastructure error with evidence, not a green
  test.

# A8. Artifacts and reports

| Artifact                          | What it keeps                                                     |
|-----------------------------------|-------------------------------------------------------------------|
| apple-preflight                   | Toolchain inventory, simulator inventory, diagnostic logs         |
| maui-ios-sim / avalonia-ios-sim   | Unsigned simulator .app (in a ZIP or tar to keep permissions)     |
| maui-maccatalyst / avalonia-macos | Unsigned .app (CI/diagnostics only)                               |
| ios-e2e-maui / ios-e2e-avalonia   | TRX, per-test video, PNG, XML, crash/console logs, Allure results |
| macos-e2e-\*                      | Preflight evidence, UI results where available                    |
| apple-allure-html                 | A merged report without mixing platform labels                    |

In all test jobs: upload-artifact with if: always(), retention-days: 14
(as in the original specification). The report job — always() and resilience to
missing artifacts. Do not upload user tokens, private
images, passwords and API keys in screenshots, XML, videos or logs.

# A9. Test data and safety conditions

- Strictly offline smoke/head scenarios or a separate mock/fake
  provider. The app must not require OpenRouter/fal.ai/Together
  keys for a successful CI test.

- If real services are needed after all — a separate future
  specification, isolated secrets + budget/quotas; at this stage
  forbid real paid requests.

- Unique Bundle IDs for MAUI and Avalonia iOS and for the two macOS
  heads; independent data folders and settings. Choose the identifiers
  consistently with the current Android coexistence.

- Do not do an unattended release, signing or notarization with this workflow.
  Leave the existing release workflow unchanged, except a possible
  hand-over of an artifact with an explicitly agreed extension.

# A10. An implementation plan without conflicts with the current work

6.  P0: take an inventory of projects/TFMs and tools; build the
    preflight on hosted macOS. The result: a table of supported
    Xcode/.NET combinations.

7.  P1: green unit-tests + Avalonia.Headless on Ubuntu; add shared
    UI TestIds, keeping the IDs from the original specification if they exist.

8.  P2: four Apple compile jobs. For Avalonia iOS create a minimal
    host that connects the existing UI/DI structure; prove the
    compilation.

9.  P3: iOS smoke through simctl for both apps; keep the
    screenshot/launch/crash evidence; the stability of 3 consecutive
    runs.

10. P4: Appium XCUITest, NUnit, video + Allure for both iOS apps, 3–5
    stable scenarios; then PR gating.

11. P5: the macOS GUI preflight, launching/a screenshot of the two .app bundles, depending on the result —
    GUI automation. Problems of the hosted runner are recorded as a blocker, and a
    green E2E is not promised.

# A11. Acceptance criteria

- [ ] Windows/Android from the original document keep working without
  regression.

- [ ] Actions has no Apple certificates, profiles, Apple ID, keys and
  other new mandatory secrets.

- [ ] Each of the four platforms has a separate compile status;
  an unfinished Avalonia iOS host is not masked as a success.

- [ ] On an Xcode/.NET incompatibility an explicit blocker with the versions appears,
  not a false pass.

- [ ] Both iOS apps install and open in the iOS Simulator;
  evidence is kept after a failed job.

- [ ] Unit + Avalonia Headless pass on a PR; iOS E2E provides
  NUnit/TRX/Allure, video, the UI tree and screenshots.

- [ ] On macOS an interactive UI test is not counted as successful until
  the preflight passes.

- [ ] Reports and videos contain no real tokens or user
  secrets.

- [ ] No IPA/DMG is published as a ready release: only unsigned
  CI artifacts.

# A12. Dependencies, open decisions and references

Before implementation, determine the current csproj paths, the SDK version in CI,
valid macOS runners/Xcode, a compatible iOS Simulator runtime, Bundle
IDs and the level of Catalyst testing. These parameters cannot be reliably
assigned from the original Windows/Android specification alone.

Official references:
https://docs.github.com/en/actions/using-github-hosted-runners/using-github-hosted-runners
; https://learn.microsoft.com/en-us/dotnet/maui/ ;
https://docs.avaloniaui.net/docs/testing/setting-up-the-headless-platform
; https://appium.io/docs/en/latest/ ;
https://developer.apple.com/documentation/xcode/running-your-app-in-simulator-or-on-a-device

The link to the original document: A3 ↔ §2, §4, §9; A7–A8 ↔ §5–8; A11 ↔ §10;
A9–A10 ↔ §11. This addendum does not rewrite the original specification.
