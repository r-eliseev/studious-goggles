# 0030. Apple platforms in CI: unsigned builds, the iOS Simulator and macOS

Status: accepted
Date: 2026-11-07

## Context
The owner sent the addendum "MAUI + Avalonia — Apple CI/E2E" to the UI test specification (ADR 0029) and asked to re-check and plan it. Goal: without an Apple ID, certificates, profiles and secrets, get four builds on GitHub runners:
- MAUI iOS;
- MAUI Mac Catalyst;
- Avalonia iOS;
- Avalonia macOS.

Then a launch in the iOS Simulator and UI tests. Windows and Android from ADR 0029 do not change.

Facts checked on 2026-11-07:
- **Xcode on the runners.** `macos-26-arm64` (image 20261026.0455.2) has Xcode 26.6 (the default), 26.5, 26.4.1, 26.3, 26.2, 26.1.1 and 26.0.1. iOS simulators: 26.2, 26.4, 26.5. .NET SDK: up to 10.0.403.
- **The Apple workload.** The .NET blog of 2026-10-19 says that the released .NET 10 iOS and Mac Catalyst workloads (27.0.10722) require Xcode 27.0. `docs/knowledge/dev-environment.md` records that on 2026-10-29 the Mac Catalyst SDK 27.0 required Xcode 27.1. Because of this the Mac Catalyst build was removed from the release (`release.yml`, the comment at line 290; `v0.1.3` failed on it).
- **Conclusion.** With the default workload the MAUI iOS and Mac Catalyst builds will not pass on GitHub runners. Avalonia iOS is also built by the `ios` workload, so the limit is the same. Avalonia macOS (`osx-arm64`, plain `net10.0`) does not depend on Xcode and already builds in `release.yml`, but as a folder, not as a `.app`.
- **Ubuntu tests already exist.** The `unit-tests` and `avalonia-headless` jobs from the addendum already exist: `core` and `avalonia` in `.github/workflows/build.yml`.
- **Appium drivers (npm).** `appium-xcuitest-driver` 13.x requires the Appium 4 beta; the latest version for Appium 3 is 12.22.8. `appium-mac2-driver` 4.6.1 works with Appium 3.
- **Avalonia iOS.**
  - `Avalonia.iOS` is on nuget.org: 12.1.4 — the same version as in the project.
  - There is no iOS head in the repository.
  - `docs/product.md` lists Avalonia only for the Linux desktop and Android. So Avalonia iOS is a new app, and this addendum makes the decision about it.
- **Avalonia markup.** `AutomationProperties.Name` occurs 108 times, `AutomationProperties.AutomationId` not once.
- **Cost.** The repository is public, so standard GitHub runners, including macOS, are free for it.

## Decision
1. **A separate workflow** `.github/workflows/apple-ci.yml`:
   - runs on `pull_request`, `push` to `main` and `workflow_dispatch`;
   - `permissions: contents: read`;
   - no secrets, publishes nothing.
   `release.yml` does not change. There is no need to duplicate `unit-tests` and `avalonia-headless`: `core` and `avalonia` from `build.yml` play their role.
2. **First a tool check (preflight, task 244).** It produces a table of combinations and looks for a workload set (`dotnet workload install … --version <set>`) compatible with the runner's Xcode 26.x. Example: a set with macios `dotnet-10.0.1xx-xcode26.6-*`. If such a set builds MAUI 10.0.110, the iOS and Catalyst builds pin it. If not, the job explicitly writes `blocked: toolchain mismatch (Xcode <versions>, workload <version>)` and fails. Such a job does not block a PR (`continue-on-error: true`), but does not count as a success either.
3. **Avalonia iOS is a new head** `ImageGen/ImageGen.Avalonia.iOS`:
   - built like the Android head (ADR 0026): desktop sources are included by links, platform services are its own;
   - bundle id `com.eliseev.imagegen.avalonia`, as for Avalonia Android;
   - built only for the simulator and not published.
4. **Avalonia macOS** is a `.app` built from `dotnet publish -r osx-arm64 --self-contained` by a packaging script. Bundle id `com.eliseev.imagegen.avalonia`, without a Developer ID. The SDK on a Mac does the ad-hoc signing (see `docs/knowledge/dev-environment.md`).
5. **iOS Simulator checks** with `xcrun simctl`:
   - the device is created and deleted for each job, the UDID is found dynamically;
   - the mock config is put into the app's data container (`simctl get_app_container … data`);
   - the mock's root certificate is installed through `simctl keychain … add-root-cert` (the same HTTPS mock as in task 243);
   - the video is recorded with `simctl io recordVideo`.
6. **iOS UI tests** — NUnit + Allure, as in ADR 0029. Driver: Appium 3.9.0 + `xcuitest@12.22.8`. One project `ImageGen.E2E.iOS` for two apps: the app is chosen by a variable, the scenarios and `TestIds` are shared. Locators follow the real accessibility tree: for Avalonia, first check what `AutomationProperties.AutomationId` turns into.
7. **macOS GUI** — first a preflight: is there a session, does `screencapture` give a meaningful frame, are there Accessibility permissions. Only if it passes — `mac2@4.6.1`. Until then macOS UI tests do not count as successful.
8. **Report** — the `apple-report` job: an Allure HTML report for the Apple platforms. The approach and the counting script come from task 242.

## Alternatives
- **Wait for Xcode 27 on the runners.** The date is unknown; until then the iOS and Catalyst builds would just be red. A check that looks for a compatible workload set gives a result sooner.
- **Our own macOS runner.** Contradicts the "only GitHub runners" condition.
- **Separate E2E projects for MAUI iOS and Avalonia iOS.** The scenarios are the same, only the app differs. Hence one project with an app choice.
- **Not doing Avalonia iOS.** The addendum asks for it explicitly, and comparing the two heads on mobile is the goal of ADR 0026.

## Consequences
- CI on a PR gets longer by the time of the macOS jobs. By how much is unknown, task 244 measures it.
- While the runners' Xcode does not match the workload, the iOS and Catalyst statuses will be "blocked". This is visible in the PR, not hidden.
- A third Avalonia head (iOS) appears. Its platform services — Keychain, paths, saving pictures, sharing logs — must be maintained.
- `docs/product.md` gets a line about Avalonia iOS and macOS for CI. There is no publishing or signing.

Checked by task 244 ([run 42369719262](https://github.com/r-eliseev/studious-goggles/actions/runs/42369719262)): `macos-26` has Xcode 26.6; the default workload of .NET SDK 10.0.403 requires Xcode 27.1 and does not build iOS/Catalyst. The set `10.0.302.0` from the release `dotnet-10.0.1xx-xcode26.6-10301` builds both targets with zero warnings and errors if the installed Xcode is selected directly (`sudo xcode-select -s /Applications/Xcode_26.6.app/Contents/Developer`). Summary and timings: `docs/knowledge/apple-ci.md`.
