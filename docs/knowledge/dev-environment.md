# Среды разработки

Общий обзор: где какой проект собирается и что умеет каждая среда. Записано 2026-10-27. Репозиторий используется из четырёх сред:

- **Windows-машина** — полная среда, включая цель Windows у MAUI. Описание: `docs/knowledge/windows-machine.md`.
- **Облачная сессия Claude Code** (Linux-контейнер) — без цели Windows у MAUI, с целью Android. Описание: `docs/knowledge/claude-cloud-session.md`.
- **Kiro Web** (браузерная сессия, Linux) — без цели Windows у MAUI, Android только собирается (эмулятора нет). Корень read-only, инструменты в `/projects/sandbox/.toolcache`. Описание: `docs/knowledge/kiro-web-session.md`.
- **VPS** (постоянная Linux-машина) — без цели Windows у MAUI, с целью Android; окружение переживает сессию. Собирает и тестирует все Linux-проекты, включая Avalonia (headless-тесты и запуск окна под `Xvfb`). Описание: `docs/knowledge/vps-session.md`.

Пункты с пометкой «не проверено» ещё не проверялись в реальной среде.

## Что где собирается
| Проект | Windows-машина | Облачная сессия (Linux) | Kiro Web (Linux) | VPS (Linux) |
|---|---|---|---|---|
| `ImageGen.Core`, `ImageGen.Core.Tests` | да | да, проверено | да, проверено (59/59 тестов) | да, проверено (356/356 тестов) |
| `ImageGen.Presentation`, `ImageGen.Presentation.Tests` | да | да, проверено (112/112 тестов) | не проверено | да, проверено (277/277 тестов) |
| `ImageGen.Avalonia`, `ImageGen.Avalonia.Tests` (десктоп, headless) | да, проверено 2026-10-28 (45 тестов, 1 пропущен — Secret Service; приложение запускается) | да, проверено (4/4 тестов; запуск окна без дисплея не проверен) | не проверено | да, проверено (118/120 тестов, 2 пропущены — Secret Service и DPAPI); приложение запускается под `Xvfb` |
| Avalonia Android probe из задачи 094 (связанные экраны и сервисы) | не проверено | не проверено | не проверено | да, проверено 2026-10-30: Debug build и Release APK `android-arm64`; запуск на устройстве не проверен |
| `ImageGen.Data`, `ImageGen.Data.Tests` | да | да, проверено | да, проверено (16/16 тестов) | да, проверено (70/70 тестов) |
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
- Отладочный трейс (задача 085) включается отдельно от уровня: `IMAGEGEN_DEBUG_TRACE=1` или `{"debugTrace": true}` в `logging.json`. Пишет в `{папка данных}/debug/<дата>/trace.jsonl` и картинки рядом; очистки нет (неизвестно, нужна ли). Спецификация: `docs/specs/debug-trace.md`.
- Папка данных Avalonia задаётся переменной `IMAGEGEN_DATA_DIR` (второй профиль для ручной проверки). Тесты `ImageGen.Avalonia.Tests` её не используют: они работают во временной папке и с хранилищем ключей в памяти (`App.HostOverrides`, задача 048).

## Релиз
- Workflow `.github/workflows/release.yml` запускается на тег `vMAJOR.MINOR.PATCH` (например, `v0.1.0`) и создаёт GitHub Release с заметками из `scripts/release-notes.sh`. Заметки перечисляют задачи, ставшие `done` после предыдущего по версии тега `vX.Y.Z` (по файлам задач и `tasks/DONE.md` в версии тега; папка задачи не важна), и заканчиваются ссылкой на сравнение коммитов; у первого тега — все завершённые задачи. Тот же текст локально: `git fetch --tags && scripts/release-notes.sh <тег> [<предыдущий тег>]`. Ручной запуск (`workflow_dispatch`, «Run workflow» в Actions) собирает те же пакеты как артефакты запуска, без релиза, с версией `0.0.<номер запуска>`: так проверяют workflow до тега.
- Shell-регрессия release notes (в том числе пустой предыдущий набор done-задач): `bash scripts/tests/release-notes-empty-previous.sh`.
- Пакеты (решение владельца 2026-10-29: обе оболочки под все платформы):
  - Avalonia, self-contained: `linux-x64`, `linux-arm64` (`.tar.gz`), `win-x64`, `win-arm64` (`.zip`), `osx-x64`, `osx-arm64` (`.tar.gz`, собираются на macOS-раннере: `osx-arm64` без подписи ad-hoc не запускается, а SDK подписывает apphost только на Mac).
  - MAUI Windows: `win-x64`, `win-arm64`, без MSIX (`WindowsPackageType=None`), с Windows App SDK внутри, `.zip`.
  - MAUI Android: `.apk`, ставится вручную. Подпись — постоянный ключ из секретов репозитория `ANDROID_KEYSTORE_BASE64` (keystore в base64), `ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASSWORD` (задача 163): тогда обновление ставится поверх с сохранением данных. Пока секретов нет (задача 165), сборка подписывает APK отладочным ключом сборочной машины, как раньше, с предупреждением в журнале: такой APK поверх другого релиза не ставится. До v0.1.5 включительно релизы подписаны отладочными ключами, у каждого свой сертификат: переход на постоянный ключ требует один раз удалить приложение (данные стираются, `allowBackup=false`). Проверить сертификат: `apksigner verify --print-certs <apk>`. С того же релиза пакет Android — `com.eliseev.imagegen` (задача 164; раньше `com.companyname.imagegen.maui`): он ставится отдельным приложением, старое удаляется вручную; данные не переносятся. У Windows-цели MAUI идентификатор прежний до миграции по 030.
  - MAUI Mac Catalyst и iOS не публикуются (решение владельца 2026-10-29). Текущий workload `maui` (Mac Catalyst SDK 27.0) требует Xcode 27.1, а на раннерах GitHub `macos-latest` и `macos-26` на 2026-10-29 только Xcode 26.x (до 26.6). На Mac есть пакеты Avalonia `osx-x64` и `osx-arm64`. iOS к тому же без сертификата и профиля Apple на устройство не ставится.
  - Если вернуть Mac Catalyst: на macOS ставить workload `maui` целиком, а не `maui-maccatalyst` (restore проверяет все целевые платформы проекта, иначе `NETSDK1147`), и раннер с нужным Xcode.
- Версия MAUI: `ApplicationDisplayVersion` из тега, `ApplicationVersion` — номер запуска workflow (Android требует целое, растущее от релиза к релизу).
- MAUI Windows публикуется с `-p:ImageGenWindowsRid=<rid>`; проект передаёт его в `RuntimeIdentifier` только для Windows TFM, не затрагивая project references и остальные платформы. Архив `win-arm64` релиза `v0.1.4` фактически содержал x64 PE-файлы. Workflow проверяет поле Machine PE-заголовков перед упаковкой; подробности и номер проверки — в [справочнике MAUI](maui.md#публикация-maui-под-windows-с-rid).
- Проверено запуском по тегу `v0.1.3` (2026-10-29): все пакеты Avalonia, MAUI Windows `win-x64`/`win-arm64` и Android собираются. Локально на Windows опубликованный MAUI `win-x64` запускается. Пакеты под arm64 и macOS на устройствах не запускались.
- Первый удачный релиз — `v0.1.2` (2026-10-29, только Avalonia `linux-x64` и `win-x64`); `v0.1.0` и `v0.1.1` упали на шаге публикации `win-x64`, `v0.1.3` — на сборке Mac Catalyst (релиза нет).
