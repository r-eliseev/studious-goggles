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
