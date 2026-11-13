---
id: 246
status: done
assignee:
claimed:
created: 2026-11-07
---

# Avalonia iOS: simulator application host

## Description
The Apple addendum (ADR 0030, section 3) calls for a separate Avalonia iOS host that does not reuse the desktop entry point. Structure it like the Android host (ADR 0026): link source files from `ImageGen.Avalonia` and provide platform-specific services. Build and launch for iOS Simulator without signing; there is no distribution.

Depends on: 244 (working `ios` workload and Xcode set; if 244 reports “blocked”, the host can be implemented but not built, so set this task to `blocked` with that reason after step 1).
Environment: GitHub Actions `macos-26` (the `ios` workload cannot be installed locally on Linux); implement locally, but `dotnet format` cannot run on Linux without the workload, so run `dotnet format … --verify-no-changes` in a macOS CI step.

## Acceptance criteria
- Project `ImageGen/ImageGen.Avalonia.iOS/ImageGen.Avalonia.iOS.csproj`: `net10.0-ios`, `SupportedOSPlatformVersion` 15.0, `ApplicationId` `com.eliseev.imagegen.avalonia`, `ApplicationTitle` Omnilite, packages `Avalonia.iOS` and `Avalonia.Themes.Fluent` (use the Avalonia version from `ImageGen/Directory.Packages.props`, currently 12.1.4; add `Avalonia.iOS` at the same version). Link the same `Compile`/`AvaloniaXaml`/`AvaloniaResource` sources as `ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj` (lines 28–33), with the same exclusions (`MainWindow`). `MobileApp.axaml(.cs)` is a copy of the Android version with iOS services (do not extract shared code in this task). Add it to `ImageGen/ImageGen.slnx`.
- Use the Avalonia iOS 12 entry-point template (`Main.cs` with `UIApplication.Main`, `AppDelegate : AvaloniaAppDelegate<MobileApp>`); compare with the official `dotnet new avalonia.xplat` template version 12.1.x. If the template is unavailable, use https://docs.avaloniaui.net (iOS section) and record the source.
- Platform services in `ImageGen/ImageGen.Avalonia.iOS/Platform/` (based on the Android host service list in `MobileApp.axaml.cs`, around lines 42–105):
  - `IosAppPaths : IAppPaths` — data folder is `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)` + `ImageGen` (inside the app container), logs at `<data>/logs`.
  - `IosSecretStore : ISecretStore, ISecretPersistence` — use Keychain through `Security.SecRecord` (`SecKind.GenericPassword`, `Service` = `com.eliseev.imagegen.avalonia`, `Account` = secret name from `SecretFileNames`), `SecAccessible.AfterFirstUnlockThisDeviceOnly`; behavior matches `AndroidSecretStore` (same interface and same save-warning behavior on failure). On an unsigned simulator, Keychain may return `-34018` (missing entitlements): if so, add `Entitlements.plist` with `keychain-access-groups` for the simulator (`CodesignEntitlements` only for `iossimulator`) and record this in `docs/knowledge/apple-ci.md`; if that does not help, set status to `blocked`.
  - `IImageSaver` — use `AvaloniaImageSaver` (system save dialog through `StorageProvider`) as the fallback used by Android before API 29 (task 233); saving to Photos is out of scope.
  - `IFolderLauncher` — `UnsupportedFolderLauncher` (copy from the Android host).
  - `ILogSharer` — `IosLogSharer` through `UIActivityViewController` with the log file; if this is difficult in the simulator, use a stub with “Not supported on iOS yet” (resource) and record it in “Comments”.
  - `IMediaPlayerService` (if it exists at task start from 219/224) — stub with “Not supported on iOS yet”.
- Icon resource: use `ImageGen/ImageGen.Avalonia.Android/Icon.png` (or task 234's result) as `Assets.xcassets/AppIcon` with one 1024×1024 image if the source is large enough; otherwise, omit the icon and record that.
- CI build: run `dotnet build ImageGen/ImageGen.Avalonia.iOS/ImageGen.Avalonia.iOS.csproj -c Debug -r iossimulator-arm64` as a temporary step in `apple-preflight` (before task 245) with the same `blocked` rules; then manually install and launch in the simulator with `xcrun simctl install` + `launch`, capture a screenshot after 15 seconds, and upload it as an artifact. The screenshot must show the first-run screen; inspect it and record the result in “Report”.
- Documentation: add an “iOS host” section to `docs/knowledge/avalonia-mobile.md` (structure and unsupported features); add a line about Avalonia's third host to `docs/architecture.md` if hosts are listed; in `docs/product.md`, add “Avalonia on iOS and macOS: CI builds and tests only, no distribution (ADR 0030)” to “Platforms and shells”.

## Analysis
Reference: the owner's addendum is `docs/specs/ui-e2e-original/MAUI_Avalonia_Apple_CI_E2E_Addendum.md`; ADR 0030 and `docs/specs/apple-ci-e2e.md` take precedence if they differ.
`ImageGen/ImageGen.Avalonia.Android/` (csproj, `MobileApp.axaml.cs`, `Platform/*`), `ImageGen/ImageGen.Avalonia/Services/Secrets/SecretFileNames.cs`, Core interfaces `IAppPaths`, `ISecretStore`, `ISecretPersistence` (`rg -n "interface ISecretStore|interface ISecretPersistence|interface IAppPaths" ImageGen`), `docs/knowledge/avalonia.md` (iOS: documentation only), ADR 0008 (Keychain on macOS).

## Plan
1. Create the project scaffold and entry point; build it in the CI preflight step.
2. Add `IosAppPaths`, `IosSecretStore`, and the remaining services one at a time, each with a CI build.
3. Launch in the simulator and capture a screenshot.
4. Update documentation.

Files: `ImageGen/ImageGen.Avalonia.iOS/` (new: csproj, `Main.cs`, `AppDelegate.cs`, `MobileApp.axaml`, `MobileApp.axaml.cs`, `Info.plist`, `Entitlements.plist` if needed, `Platform/*.cs`, `Assets.xcassets`), `ImageGen/Directory.Packages.props`, `ImageGen/ImageGen.slnx`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx` and `AppResources.cs` (if the “Not supported on iOS yet” text is needed), `.github/workflows/apple-ci.yml`, `docs/knowledge/avalonia-mobile.md`, `docs/knowledge/apple-ci.md`, `docs/architecture.md`, `docs/product.md`.

### Verification
```
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
bash scripts/check.sh
# GitHub Actions: build ImageGen.Avalonia.iOS for iossimulator-arm64, dotnet format on macOS, launch screenshot
```

### Execution order (for Luna)
- First read only the files in the “Files” line and the named tests; compare signatures with current `main`.
- Follow the steps in order (ub-mode): failing test → run it and confirm it fails for the described reason → minimal fix → green test.
- Do not change files outside the “Files” line, rename existing members beyond the plan, or fix neighboring findings; those belong to separate tasks.
- If the code differs from the plan (missing method, different signature, test already exists), do not improvise; stop and write a question in “Comments”.
- Run `dotnet` commands one at a time, not in parallel. After each changed project, run `dotnet format <project> --verify-no-changes`.
- In the “Report”, record what changed, commands and results, and differences from the plan.

## Report

### 2026-11-10, codex-gpt6
- Job `build-avalonia-ios` passed in scheduled run [42794177774](https://github.com/r-eliseev/studious-goggles/actions/runs/42794177774).
- Simulator launch showed the first-run “Connect OpenRouter” screen in run [42405774295](https://github.com/r-eliseev/studious-goggles/actions/runs/42405774295). Safe-area spacing was not visually checked on a real device; previously reported black bars do not prevent launch.

### 2026-11-07, codex-gpt6 (in progress)
- The first CI build exposed the `SecKeyChain.QueryAsRecord` signature (`SecRecord` is returned and `SecStatusCode` is passed through `out`), a nullable UIKit view, and obsolete `UIApplication.KeyWindow`; these were fixed.
- Locally suppressed IL2026 from dynamic Avalonia theme resource loading only in the iOS project, where the theme is selected by URI at runtime.
- The second CI build passed C# compilation, then showed that XAML resources expect the `ImageGen.Avalonia` assembly, as in the Android host. Renamed the iOS project's assembly; another build and simulator launch were pending.
- Logs from the rerun: [42371977271](https://github.com/r-eliseev/studious-goggles/actions/runs/42371977271), job `build-avalonia-ios`.
- Run [42373330934](https://github.com/r-eliseev/studious-goggles/actions/runs/42373330934): host C# compilation succeeded, but the iOS linker failed with `IL2104`/`IL2121` and `NETSDK1144`; packaging then failed because the app was missing. For the CI-only simulator build, switch to `TrimMode=copy` (no size reduction is needed for this smoke artifact), remove the stale broad IL2026 suppression, and avoid labeling arbitrary build errors as a toolchain block. New-run verification was pending.
- Run [42374038441](https://github.com/r-eliseev/studious-goggles/actions/runs/42374038441): `TrimMode=copy` reduced the failure to the one known IL2026 warning for runtime-selected bundled theme XAML. Restored its iOS-project-only suppression; CI verification was pending.

## Comments

### 2026-11-07, claude-audit
Created from the Apple addendum (A2, A5): the repository had no Avalonia iOS host; Avalonia.iOS 12.1.4 was available on nuget.org. Services follow the Android host list. Plan for Luna.

### 2026-11-07, owner (in chat, recorded by claude-audit)
The owner's “MAUI + Avalonia — Apple CI/E2E” addendum (original: `docs/specs/ui-e2e-original/`). The plan was approved under the earlier instruction “approve all tasks with plans”. The owner explicitly requested the new `apple-ci.yml` workflow as part of this addendum (ADR 0030).
