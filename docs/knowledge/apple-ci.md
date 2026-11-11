# Apple CI

## Preflight 244

[Successful run 42369719262](https://github.com/r-eliseev/studious-goggles/actions/runs/42369719262), the PR #85 branch, job `apple-preflight` — 5 min 9 s. Runner `macos-26`, architecture `arm64`, macOS 26.7.2 (build 25G332), Xcode 26.6 (17F113), .NET SDK 10.0.403. iOS Simulator runtimes 26.2, 26.4 and 26.5 are available.

| Option | SDK and workload | MAUI iOS Simulator | MAUI Mac Catalyst | Time |
|---|---|---|---|---:|
| Default | .NET SDK 10.0.403, `dotnet workload install maui` (workload manifests 10.0.400) | `blocked`: the workload installed iOS 27.1.10018, Xcode 27.1 is required | `blocked`: the workload installed Catalyst 27.0.10742, Xcode 27.1 is required | 85 s |
| Compatible set | SDK 10.0.302; release `dotnet-10.0.1xx-xcode26.6-10301`; workload set `10.0.302.0`; iOS/Catalyst SDK 26.5.10301 | Build succeeds, 0 warnings/errors | Build succeeds, 0 warnings/errors | 158 s |

For pinned builds `xcode-select` must point directly to `/Applications/Xcode_26.6.app/Contents/Developer`. Selecting the symlink `/Applications/Xcode.app/Contents/Developer` made `xcrun` fail to find `actool`; after selecting the specific installed version both builds passed. The workflow writes the full inventory, the output of the attempts, the last 80 lines of each attempt and the simulator device types/runtimes/devices into the `apple-preflight` artifact (14 days).

## Separate builds 245

As of 2026-11-10, scheduled run [42794177774](https://github.com/r-eliseev/studious-goggles/actions/runs/42794177774) passed all four jobs: `build-maui-ios`, `build-maui-catalyst`, `build-avalonia-ios`, and `build-avalonia-macos`. Avalonia iOS formatting and artifact packaging/upload also passed.

The first separate workflow [42371375200](https://github.com/r-eliseev/studious-goggles/actions/runs/42371375200) confirmed MAUI iOS Simulator (artifact `maui-ios-sim`, 12:12:29–12:15:25 UTC) and the Avalonia macOS `.app` (12:11:53–12:12:26 UTC). MAUI Mac Catalyst built without warnings, but the packaging step chose the wrong `Info.plist` (12:11:47–12:13:56 UTC); Avalonia iOS revealed C# and XAML errors, the fixes wait for a repeated check. The success of the whole workflow does not mean the success of each platform: look at the separate jobs.

### Avalonia iOS Simulator — ILLink

In run [42373330934](https://github.com/r-eliseev/studious-goggles/actions/runs/42373330934), job `build-avalonia-ios`, C# compilation succeeded but ILLink ended with `IL2104`, `IL2121`, and `NETSDK1144`. This is separate from the Xcode/workload compatibility check: MAUI iOS/Catalyst and Avalonia macOS jobs passed in the same run. The CI-only simulator build now uses `TrimMode=copy` so the smoke app does not depend on size trimming, and package verification runs only after a successful build. New-run result pending.

Run [42374038441](https://github.com/r-eliseev/studious-goggles/actions/runs/42374038441) confirmed `TrimMode=copy` removes the grouped dependency/SDK diagnostics and exposes only IL2026 on `AvaloniaAppearanceApplier.Apply`, which dynamically selects one of the bundled theme resource URIs. The iOS host now suppresses that one linker warning locally; this differs from the previous broad build failure and awaits another runner check.

## Simulator and macOS smoke failures after merge

### Three latest scheduled smoke runs (2026-11-08 through 2026-11-10)

| Run | MAUI iOS | Avalonia iOS | macOS GUI |
|---|---|---|---|
| [42465262394](https://github.com/r-eliseev/studious-goggles/actions/runs/42465262394) | success | success | success |
| [42596262790](https://github.com/r-eliseev/studious-goggles/actions/runs/42596262790) | success | success | success |
| [42794177774](https://github.com/r-eliseev/studious-goggles/actions/runs/42794177774) | success | **failed after launch** | success |

The latest Avalonia artifact reports iOS 26.5 / iPhone 17 Pro, `launched=true`, `aliveAfter30s=false`, `crashReports=0`; inspect its `launch.log` to identify the exit cause. Thus the three-consecutive-success criterion is met for MAUI, but not Avalonia. All three macOS artifacts report `guiSession=true`, `accessibilityOk=true`, `nonUniformRatio` about 0.999, zero crash reports, and no app failures. This confirms the hosted environment meets the preflight threshold; a separate Mac2 UI-test task is proposed in `docs/status.md`.

On `main` run [42381579325](https://github.com/r-eliseev/studious-goggles/actions/runs/42381579325), all Apple compile jobs succeeded and all three smoke jobs ran. The macOS preflight stopped while parsing `sips` output: the runner produced a 32-bit top-down BMP with `BI_BITFIELDS` compression and standard BGRA channel masks (`compression=3`). The pixel parser now accepts this uncompressed bitfield form; a saved runner screenshot measures a nonuniform ratio of `0.9991188`.

On run [42386890196](https://github.com/r-eliseev/studious-goggles/actions/runs/42386890196), both iOS simulator apps built successfully, but crashed after launch with `SIGKILL (Code Signature Invalid)`, namespace `CODESIGNING`, indicator `Invalid Page`. The faulting image in both reports is Apple's `dyld_sim` (UUID `ba544d9a-46ab-3c59-a00f-aba1479d2079`), while each app executable has its own UUID. The crash occurs as `dyld_sim` maps a Mach-O image; this does not establish that the app bundle's signature is invalid. Ad-hoc signing the extracted app and enabling signing overrides at build time did not resolve it. The workflow therefore returns to the .NET SDK's default unsigned simulator build while this remains an Apple runner/runtime issue.

The same run confirmed `desktop-metrics.json` reports a nonuniform ratio of `0.9991404`, and both Avalonia and Catalyst apps stayed running and produced screenshots. The macOS job failed afterward because `set -u` treated the empty `app_failures` array as unbound when creating `summary.json`; the script now expands it with an empty default. Hosted confirmation of this fix is pending.

Run [42392573546](https://github.com/r-eliseev/studious-goggles/actions/runs/42392573546) tested both apps on the installed iOS 26.4 runtime as well as the default iOS 26.5 runtime. Both versions produced the same `dyld_sim` / `CODESIGNING: Invalid Page` failure, so changing only the runtime version is not a fix. A similar open report exists for .NET MAUI on Xcode 26.5/macOS 26.5: [dotnet/maui#35792](https://github.com/dotnet/maui/issues/35792). The first Xcode 26.4 toolchain comparison selected an incomplete `Xcode_26.4.app` alias whose macOS SDK is missing; the workflow now selects the installed `Xcode_26.4.1.app` explicitly. A matching .NET 10.0.203 build/smoke comparison on iOS 26.4 failed because the SDK did not find a signing identity. The workflow now explicitly selects the ad-hoc identity `-`; the run will test SDK-generated signatures without rewriting them after build.

## Avalonia iOS in the simulator: what prevented the launch (2026-11-07)

- **`-p:TrimMode=copy` breaks the `AppDelegate` mapping.** The runtime creates the generic `Avalonia.iOS.AvaloniaAppDelegate\`1`: with the default registrar this is a `MemberAccessException`, with the static one "because the type is generic". At the same time `_OBJC_CLASS_$_AppDelegate` is present in the binary (`llvm-nm`). Without `TrimMode=copy` everything works. The linker warnings for which the flag was added must not be errors: `-p:ILLinkTreatWarningsAsErrors=false`, because `Directory.Build.props` enables `TreatWarningsAsErrors`.
- **`AvaloniaXamlLoader.Load(this)` in the iOS head's `MainView` left `PageHost` empty.** `InitializeComponent()` helps.
- **Links to resources from the desktop project.** `Link` must include `Fonts/`: `Assets/Fonts/%(RecursiveDir)%(Filename)%(Extension)`. `%(RecursiveDir)` is only the part of the path after `**`.
- **The cause of a crash is shown by the `launch.log` of the smoke artifact.** On a `SIGABRT` from an unhandled managed exception there is no crash report (`crashReports=0`).
- **Checking the process at 15 s is unreliable.** MAUI was sometimes still on the splash screen at 15 s, so the check at 30 s decides.
- **`launchctl list | grep -q` under `pipefail` fails falsely.** `grep -q` exits on the first match, `simctl spawn` gets `SIGPIPE` ("signal 13: Broken pipe"), and the check considers a live app dead (run 42409893841). The output is saved to a variable first. Verified by run [42412288939](https://github.com/r-eliseev/studious-goggles/actions/runs/42412288939): both iOS smokes are green.
