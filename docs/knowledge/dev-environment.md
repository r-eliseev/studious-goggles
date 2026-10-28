# Среды разработки

Общий обзор: где какой проект собирается и что умеет каждая среда. Записано 2026-10-27. Репозиторий используется из четырёх сред:

- **Windows-машина** — полная среда, включая цель Windows у MAUI. Описание: `docs/knowledge/windows-machine.md`.
- **Облачная сессия Claude Code** (Linux-контейнер) — без цели Windows у MAUI, с целью Android. Описание: `docs/knowledge/claude-cloud-session.md`.
- **Kiro Web** (браузерная сессия, Linux) — без цели Windows у MAUI, Android только собирается (эмулятора нет). Корень read-only, инструменты в `/projects/sandbox/.toolcache`. Описание: `docs/knowledge/kiro-web-session.md`.
- **VPS** (постоянная Linux-машина) — без цели Windows у MAUI, с целью Android; окружение переживает сессию. Описание: `docs/knowledge/vps-session.md`.

Пункты с пометкой «не проверено» ещё не проверялись в реальной среде.

## Что где собирается
| Проект | Windows-машина | Облачная сессия (Linux) | Kiro Web (Linux) | VPS (Linux) |
|---|---|---|---|---|
| `ImageGen.Core`, `ImageGen.Core.Tests` | да | да, проверено | да, проверено (59/59 тестов) | да, проверено (193/193 тестов) |
| `ImageGen.Presentation`, `ImageGen.Presentation.Tests` | да | да, проверено (112/112 тестов) | не проверено | не проверено |
| `ImageGen.Avalonia`, `ImageGen.Avalonia.Tests` (десктоп, headless) | да, проверено 2026-10-28 (45 тестов, 1 пропущен — Secret Service; приложение запускается) | да, проверено (4/4 тестов; запуск окна без дисплея не проверен) | не проверено | не проверено |
| `ImageGen.Data`, `ImageGen.Data.Tests` | да | да, проверено | да, проверено (16/16 тестов) | да, проверено (38/38 тестов) |
| `ImageGen.Maui`, цель Android (`net10.0-android`) | да | да, проверено (APK собран на шаблоне) | сборка да (не проверено на проекте); запуск/отладка нет — нет эмулятора, `adb`, `/dev/kvm` | сборка да, проверено (APK собран на проекте); запуск/отладка — только на реальном устройстве по сети через `adb connect`, эмулятор не работает (нет KVM) |
| `ImageGen.Maui`, цель Windows (`net10.0-windows…`) | да | нет, проверено (XAML-компилятор только под Windows) | нет, проверено (как в облачной сессии) | сборка да, проверено, но через Wine и неофициально (1 ч 45 мин); запуск и отладка нет — WinUI 3 под Wine не работает |

Отладка GUI: Windows-приложение — только на Windows-машине; Android вживую (эмулятор/устройство, точки останова, Hot Reload) — ни в облачной сессии, ни в Kiro Web (там Android только компилируется), ни на VPS (там эмулятор не запускается, но есть отладка на реальном устройстве по сети). Подробности по Kiro Web: `docs/knowledge/kiro-web-session.md`.
- Сборка всего решения (`dotnet build ImageGen/ImageGen.slnx`) и `dotnet format ImageGen/ImageGen.slnx` на Linux не запускаются, потому что решение включает MAUI-проект, а без нагрузки MAUI он не загружается. Форматирование и сборка по отдельным проектам: `dotnet format ImageGen/<Проект>/<Проект>.csproj --verify-no-changes`.
- В CI на ubuntu-latest установка нагрузки `maui-android` падала с «Disk quota exceeded», поэтому Android в CI собирается на Windows (`.github/workflows/build.yml`, задача `maui`). В облачной сессии Android-сборка проверена, но CI на ней не настроен.

## Строка «Среда» в задаче
У каждой задачи в «Описании» есть строка «Среда». Правило в `docs/tasks.md`: агент, у которого нет нужной среды, задачу не занимает.

С 2026-10-28 (решение владельца: как можно больше разработки на Linux) действует такое деление:
- **Linux или Windows** — всё, что проверяется сборкой и тестами: ядро, хранилище, `Presentation`, Avalonia (сборка, headless-тесты, запуск окна на Linux-рабочем столе), MAUI через цель Android (сборка и `dotnet format` с Android SDK). Windows-цель MAUI для таких задач проверяет CI (задача `maui` на `windows-latest`).
- **только Windows** — то, что нельзя проверить иначе: запуск и ручная проверка MAUI-приложения под Windows, поведение, которого нет на Linux (DPAPI, WinUI-специфика), правки `Platforms/Windows`.
- **человек у экрана** — ручные сценарии (задачи 016, 046).

Задача с MAUI-разметкой по умолчанию идёт на Linux: логика экранов в `Presentation`, их интерфейс можно увидеть и потыкать в Avalonia на Linux (тот же сценарий), а MAUI-разметка проверяется сборкой Android и CI. Если в такой задаче осталась только проверка, недоступная в среде (Windows-запуск, macOS, ручной экран), её не держат в `in-progress`: см. `docs/tasks.md`, «Завершение».

## Логи
Журнал приложения пишется в файлы `imagegen-yyyyMMdd.log` в папке `logs` внутри папки данных (ADR 0010). Первая строка каждого запуска называет версию, оболочку, ОС и путь к папке.
- **Avalonia, Linux:** `~/.local/share/ImageGen/logs` (проверено, `Environment.SpecialFolder.LocalApplicationData`, headless-тесты).
- **Avalonia, Windows:** `%LOCALAPPDATA%\ImageGen\logs` (проверено 2026-10-28: первая строка запуска — версия, оболочка, ОС, папки данных и логов). Внимание: до задачи 048 headless-тесты Avalonia пишут сюда же.
- **Avalonia, macOS:** неизвестно, путь не проверен (нет macOS-машины).
- **MAUI:** папка `AppDataDirectory` приложения (`FileSystem.Current.AppDataDirectory`), подпапка `logs`. Android: не проверено на устройстве (эмулятора нет). Сообщения пишутся и в `Debug`-вывод (logcat).
- Уровень: `IMAGEGEN_LOG_LEVEL=Trace|Debug|Information|Warning|Error` или `logging.json` `{"level": "Debug"}` в папке данных; по умолчанию Debug в Debug-сборке, Information в Release.
