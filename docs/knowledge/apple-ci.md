# Apple CI

## Preflight 244

[Успешный прогон 42369719262](https://github.com/r-eliseev/studious-goggles/actions/runs/42369719262), ветка PR #85, job `apple-preflight` — 5 мин 9 с. Runner `macos-26`, архитектура `arm64`, macOS 26.7.2 (build 25G332), Xcode 26.6 (17F113), .NET SDK 10.0.403. Доступны iOS Simulator runtimes 26.2, 26.4 и 26.5.

| Вариант | SDK и workload | MAUI iOS Simulator | MAUI Mac Catalyst | Время |
|---|---|---|---|---:|
| По умолчанию | .NET SDK 10.0.403, `dotnet workload install maui` (workload manifests 10.0.400) | `blocked`: workload поставил iOS 27.1.10018, требуется Xcode 27.1 | `blocked`: workload поставил Catalyst 27.0.10742, требуется Xcode 27.1 | 85 с |
| Совместимый набор | SDK 10.0.302; release `dotnet-10.0.1xx-xcode26.6-10301`; workload set `10.0.302.0`; iOS/Catalyst SDK 26.5.10301 | Сборка успешна, 0 warnings/errors | Сборка успешна, 0 warnings/errors | 158 с |

Для pinned-сборок `xcode-select` должен указывать прямо на `/Applications/Xcode_26.6.app/Contents/Developer`. Выбор symlink `/Applications/Xcode.app/Contents/Developer` привёл к тому, что `xcrun` не нашёл `actool`; после выбора конкретной установленной версии обе сборки прошли. Workflow записывает полный инвентарь, вывод попыток, последние 80 строк каждой попытки и simulator device types/runtimes/devices в артефакт `apple-preflight` (14 дней).

## Раздельные сборки 245

Первый раздельный workflow [42371375200](https://github.com/r-eliseev/studious-goggles/actions/runs/42371375200) подтвердил MAUI iOS Simulator (артефакт `maui-ios-sim`, 12:12:29–12:15:25 UTC) и Avalonia macOS `.app` (12:11:53–12:12:26 UTC). MAUI Mac Catalyst собрался без предупреждений, но упаковочный шаг выбрал неверный `Info.plist` (12:11:47–12:13:56 UTC); Avalonia iOS выявила ошибки C# и XAML, исправления ожидают повторной проверки. Успех общего workflow не означает успех каждой платформы: смотрите отдельные jobs.

### Avalonia iOS Simulator — ILLink

В run [42373330934](https://github.com/r-eliseev/studious-goggles/actions/runs/42373330934), job `build-avalonia-ios`, C# compilation succeeded but ILLink ended with `IL2104`, `IL2121`, and `NETSDK1144`. This is separate from the Xcode/workload compatibility check: MAUI iOS/Catalyst and Avalonia macOS jobs passed in the same run. The CI-only simulator build now uses `TrimMode=copy` so the smoke app does not depend on size trimming, and package verification runs only after a successful build. New-run result pending.

Run [42374038441](https://github.com/r-eliseev/studious-goggles/actions/runs/42374038441) confirmed `TrimMode=copy` removes the grouped dependency/SDK diagnostics and exposes only IL2026 on `AvaloniaAppearanceApplier.Apply`, which dynamically selects one of the bundled theme resource URIs. The iOS host now suppresses that one linker warning locally; this differs from the previous broad build failure and awaits another runner check.

## Simulator and macOS smoke failures after merge

On `main` run [42381579325](https://github.com/r-eliseev/studious-goggles/actions/runs/42381579325), all Apple compile jobs succeeded and all three smoke jobs ran. The macOS preflight stopped while parsing `sips` output: the runner produced a 32-bit top-down BMP with `BI_BITFIELDS` compression and standard BGRA channel masks (`compression=3`). The pixel parser now accepts this uncompressed bitfield form; a saved runner screenshot measures a nonuniform ratio of `0.9991188`.

On run [42386890196](https://github.com/r-eliseev/studious-goggles/actions/runs/42386890196), both iOS simulator apps built successfully, but crashed after launch with `SIGKILL (Code Signature Invalid)`, namespace `CODESIGNING`, indicator `Invalid Page`. The faulting image in both reports is Apple's `dyld_sim` (UUID `ba544d9a-46ab-3c59-a00f-aba1479d2079`), while each app executable has its own UUID. The crash occurs as `dyld_sim` maps a Mach-O image; this does not establish that the app bundle's signature is invalid. Ad-hoc signing the extracted app and enabling signing overrides at build time did not resolve it. The workflow therefore returns to the .NET SDK's default unsigned simulator build while this remains an Apple runner/runtime issue.

The same run confirmed `desktop-metrics.json` reports a nonuniform ratio of `0.9991404`, and both Avalonia and Catalyst apps stayed running and produced screenshots. The macOS job failed afterward because `set -u` treated the empty `app_failures` array as unbound when creating `summary.json`; the script now expands it with an empty default. Hosted confirmation of this fix is pending.

Run [42392573546](https://github.com/r-eliseev/studious-goggles/actions/runs/42392573546) tested both apps on the installed iOS 26.4 runtime as well as the default iOS 26.5 runtime. Both versions produced the same `dyld_sim` / `CODESIGNING: Invalid Page` failure, so changing only the runtime version is not a fix. A similar open report exists for .NET MAUI on Xcode 26.5/macOS 26.5: [dotnet/maui#35792](https://github.com/dotnet/maui/issues/35792). The first Xcode 26.4 toolchain comparison selected an incomplete `Xcode_26.4.app` alias whose macOS SDK is missing; the workflow now selects the installed `Xcode_26.4.1.app` explicitly. A matching .NET 10.0.203 build/smoke comparison on iOS 26.4 failed because the SDK did not find a signing identity. The workflow now explicitly selects the ad-hoc identity `-`; the run will test SDK-generated signatures without rewriting them after build.
