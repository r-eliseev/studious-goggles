# Apple CI

## Preflight 244

[Успешный прогон 42369719262](https://github.com/r-eliseev/studious-goggles/actions/runs/42369719262), ветка PR #85, job `apple-preflight` — 5 мин 9 с. Runner `macos-26`, архитектура `arm64`, macOS 26.7.2 (build 25G332), Xcode 26.6 (17F113), .NET SDK 10.0.403. Доступны iOS Simulator runtimes 26.2, 26.4 и 26.5.

| Вариант | SDK и workload | MAUI iOS Simulator | MAUI Mac Catalyst | Время |
|---|---|---|---|---:|
| По умолчанию | .NET SDK 10.0.403, `dotnet workload install maui` (workload manifests 10.0.400) | `blocked`: workload поставил iOS 27.1.10018, требуется Xcode 27.1 | `blocked`: workload поставил Catalyst 27.0.10742, требуется Xcode 27.1 | 85 с |
| Совместимый набор | SDK 10.0.302; release `dotnet-10.0.1xx-xcode26.6-10301`; workload set `10.0.302.0`; iOS/Catalyst SDK 26.5.10301 | Сборка успешна, 0 warnings/errors | Сборка успешна, 0 warnings/errors | 158 с |

Для pinned-сборок `xcode-select` должен указывать прямо на `/Applications/Xcode_26.6.app/Contents/Developer`. Выбор symlink `/Applications/Xcode.app/Contents/Developer` привёл к тому, что `xcrun` не нашёл `actool`; после выбора конкретной установленной версии обе сборки прошли. Workflow записывает полный инвентарь, вывод попыток, последние 80 строк каждой попытки и simulator device types/runtimes/devices в артефакт `apple-preflight` (14 дней).
