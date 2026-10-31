# Среды разработки

Общий обзор: где какой проект собирается и что умеет каждая среда. Сведения ниже собраны из проверок 2026-10-27—2026-10-30; платформенные отметки сохраняют статус проверки на эти даты.

- **Windows-машина** — полная среда, включая цель Windows у MAUI.
- **Облачная сессия Claude Code** (Linux-контейнер) — без цели Windows у MAUI, с целью Android.
- **Kiro Web** (браузерная сессия, Linux) — Android только собирается (эмулятора нет); корень read-only, инструменты были в `/projects/sandbox/.toolcache`. Kiro Web больше не используется.
- **VPS** (постоянная Linux-машина) — без цели Windows у MAUI, с целью Android; среда переживает сессию. Собирает и тестирует Linux-проекты, включая Avalonia (headless-тесты и запуск окна под `Xvfb`).

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

Отладка GUI: Windows-приложение — только на Windows-машине; Android вживую (эмулятор/устройство, точки останова, Hot Reload) недоступен в облачной сессии и Kiro Web; на VPS эмулятор не запускается, но возможна отладка на реальном устройстве по сети.
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
- **Avalonia, Windows:** `%LOCALAPPDATA%\ImageGen\logs` (проверено 2026-10-28: первая строка запуска — версия, оболочка, ОС, папки данных и логов).
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


## Windows-машина разработки

Описание среды, на которой собирается цель Windows у MAUI и проверяется интерфейс. Общий обзор сред: `docs/knowledge/dev-environment.md`. Записано 2026-10-27 по прежнему описанию среды. В ревизии облачной сессии эта машина не проверялась: всё ниже «не проверено» на самой машине.

## Что нужно
- Windows с установленными нагрузками MAUI (`maui-windows`, `maui-android`), .NET 10 SDK.
- Для Android-цели: Android SDK и JDK (не проверено на этой машине).
- Для проверки скелета (задача 016) нужен человек у экрана: интерфейс проверяется вручную.

## Что собирается только здесь
- Цель `net10.0-windows10.0.19041.0` у `ImageGen.Maui`. На Linux она не собирается: XAML-компилятор WinUI работает только под Windows (проверено в облачной сессии, см. `этот раздел «Облачная сессия Claude Code»`).
- Ручная проверка интерфейса скелета (задача 016) — только на Windows.

## Команды
- Сборка Windows: `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0`
- Сборка Android: `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android`
- Запуск: `ImageGen/ImageGen.Maui/bin/Debug/net10.0-windows10.0.19041.0/win-x64/ImageGen.Maui.exe`
- Форматирование по проекту: `dotnet format ImageGen/<Проект>/<Проект>.csproj --verify-no-changes`


## Облачная сессия Claude Code (проверка 2026-10-27)

Что реально работает в облачной сессии (Linux-контейнер Claude Code) и как поставить недостающее. Ревизия проведена 2026-10-27 на этой машине (Ubuntu 24.04, x86_64, 4 CPU, 15 ГБ RAM, около 23 ГБ свободно на диске). Пункты помечены: «проверено», «не работает», «не проверено». Общий обзор сред: `docs/knowledge/dev-environment.md`. Windows-машина описана в `этот раздел «Windows-машина разработки»`.

## Работает (проверено)
- **.NET SDK 10.0.403:** `dotnet build`, `dotnet test`, `dotnet format` проходят для ядра и хранилища.
- **NuGet:** `api.nuget.org` доступен.
- **MAUI, цель Android:** `dotnet build -f net10.0-android` собирает APK на свежем шаблоне `dotnet new maui`. Нужна платформа Android API 36.
- **Android SDK:** command-line tools ставятся с `dl.google.com`, лицензии принимаются через `yes | sdkmanager --licenses`.
- **JDK 21:** установлен, подходит для MAUI Android.
- **Chromium через Playwright:** `require('playwright').chromium.launch()` работает без установки. Не запускать `playwright install`.
- **Node 22, npm, Python 3.13, git 2.43, curl:** установлены; `registry.npmjs.org` и `pypi.org` доступны.
- **GitHub через MCP** (`mcp__github__*`): чтение PR, check runs, комментарии, создание PR и ответы в тредах.
- **Харнес:** `scripts/check.sh` проходит.

## Работает с оговорками
- **Docker:** CLI установлен (29.9.2), демон не запущен автоматически. Запуск: `dockerd > ~/dockerd.log 2>&1 &`. Docker Hub отвечает `429 Too Many Requests` на анонимные запросы, образы для сборки не тянутся.
- **`gh`:** установлен, не аутентифицирован (`GH_TOKEN` отвергается). Для GitHub использовать MCP.
- **Фоновые процессы:** `sleep` в переднем плане блокируется. Ждать через `run_in_background` или `until <условие>; do sleep 3; done` в фоне.

## Не работает (проверено)
- **MAUI, цель Windows:** `dotnet build -f net10.0-windows10.0.19041.0` на Linux не проходит. Без обхода — `NETSDK1100`. С `-p:EnableWindowsTargeting=true` и явным TFM — падает на XAML-компиляции (`XamlCompiler output file … was not created`). Компилятор WinUI XAML — Windows-инструмент. Значит, задачи 012–016 и Windows-часть 034 здесь не выполняются.
- **sqlite3 CLI:** не установлен. Не нужен: БД через `Microsoft.Data.Sqlite`. Установка `apt-get install sqlite3` не проверялась.
- **`chromium` в PATH:** нет. Использовать Playwright-версию из `/opt/pw-browsers`.
- **Эмулятор Android (запуск приложения):** `/dev/kvm` отсутствует, у процессора нет флагов `vmx`/`svm`. Замер 2026-10-27 (эмулятор 37.2.12, Android 36 Google APIs, 4 vCPU, 3 ГБ RAM, `-no-accel -gpu swiftshader_indirect`):
  - образ `arm64-v8a` не запускается: `System image must match the host architecture`;
  - образ `x86_64` стартует, но загрузка не завершилась за 31 минуту: `sys.boot_completed` не выставлен, `bootanim` в состоянии `running`. Эмулятор остановлен.
  - Вывод: отладка запуска и UI MAUI-приложения в облачной сессии не выполняется. Сборка, анализ APK и тесты логики — выполняются.

## Как поставить (нужно в каждой новой сессии: окружение не сохраняется)
Состояние не переживает сессию: SDK, Android SDK и запущенный `dockerd` ставятся заново.

```bash
# .NET 10 SDK
curl -sSL https://dot.net/v1/dotnet-install.sh -o ~/dotnet-install.sh
bash ~/dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1

# MAUI-нагрузка для Android
dotnet workload install maui-android --skip-manifest-update

# Android SDK (шаблон MAUI требует API 36)
mkdir -p ~/android-sdk/cmdline-tools && cd ~/android-sdk
curl -sSL -o clt.zip https://dl.google.com/android/repository/commandlinetools-linux-11076708_latest.zip
python3 -c "import zipfile;zipfile.ZipFile('clt.zip').extractall('cmdline-tools-tmp')"
mv cmdline-tools-tmp/cmdline-tools cmdline-tools/latest && chmod +x cmdline-tools/latest/bin/*
export ANDROID_HOME="$HOME/android-sdk"
yes | cmdline-tools/latest/bin/sdkmanager --licenses
cmdline-tools/latest/bin/sdkmanager "platform-tools" "platforms;android-36" "build-tools;36.0.0"
# сборка Android: dotnet build -f net10.0-android -p:AndroidSdkDirectory=$ANDROID_HOME

# Docker (по желанию)
dockerd > ~/dockerd.log 2>&1 &
```

Воспроизвести проверку Android: `dotnet new maui -n Probe`, затем `dotnet build -f net10.0-android` в каталоге проекта.

## Особенности облачной сессии
- **`dotnet format` для MAUI-проекта:** без Android SDK проект не загружается (`The Android SDK directory could not be found`), и `dotnet format` выводит «Required references did not load». С SDK из раздела «Как поставить» `dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes` проходит (проверено 2026-10-27). Сначала `dotnet restore` на том же проекте.
- Тесты не ходят в сеть: OpenRouter подменяется `StubHttpMessageHandler`, ключ не нужен.
- Скрипты задач (`scripts/claim-task.sh`, `scripts/release-task.sh --publish`, `scripts/new-task.sh --publish`) пишут в ветку на origin, одноимённую текущей, поэтому пуш в `main` облачной сессии не нужен. Ветку задачи нужно опубликовать до `claim-task.sh` (`git push -u origin task/NNN-имя`), а после него сделать `git pull --ff-only`. Порядок и ограничения: `docs/tasks.md`, «Начало работы», и `AGENTS.md и docs/tasks.md`, «Параллельная работа».


## VPS (проверки 2026-10-27—2026-10-29)

Постоянная Linux-машина (VPS), четвёртая среда в репозитории. Общий обзор сред: `docs/knowledge/dev-environment.md`. Записано 2026-10-27, все пункты проверены на машине в этот день.

Отличие от остальных Linux-сред: окружение **переживает сессию** — установленные SDK и Android SDK остаются на месте, заново ставить не нужно. Рецепт ниже нужен только при пересоздании машины.

Машина: Ubuntu 24.04.5 LTS, x86_64, AMD EPYC 9355P, 4 vCPU, 15 ГБ RAM, около 185 ГБ свободно на диске. Сама работает под гипервизором KVM.

## Что работает (проверено)
- **.NET SDK 10.0.403** — та же версия, что в `этот раздел «Облачная сессия Claude Code»`. Ставится в `/usr/share/dotnet` (в облачной сессии был `~/.dotnet`).
- **Ядро и хранилище:** `dotnet build` и `dotnet test` проходят. На `main` от 2026-10-29: Core.Tests 356 тестов, Data.Tests 70 — все проходят.
- **Presentation:** `ImageGen.Presentation` и `ImageGen.Presentation.Tests` собираются и тестируются на VPS: 277/277 тестов.
- **Avalonia:** `ImageGen.Avalonia` собирается, headless-тесты проходят — 118 из 120, два пропущены (Secret Service: нужен сессионный keyring; DPAPI — только Windows). **Приложение запускается** под виртуальным дисплеем `Xvfb` (см. ниже).
- **`dotnet format --verify-no-changes`** проходит по всем проектам: Core, Core.Tests, Data, Data.Tests, Presentation, Presentation.Tests, Avalonia, Avalonia.Tests, Maui.
- **MAUI, цель Android:** `dotnet build -f net10.0-android` собирает APK (`com.companyname.imagegen.maui.apk` и `-Signed.apk`), 0 предупреждений. Нагрузка `maui-android` установлена.
- **Android SDK:** command-line tools с `dl.google.com`, лицензии приняты, `platforms;android-36`, `build-tools;36.0.0`, `platform-tools` 37.0.1, пакет `emulator`.
- **JDK 21** (openjdk-21-jdk-headless, `/usr/lib/jvm/java-21-openjdk-amd64`) — подходит для MAUI Android.
- **Xvfb** — виртуальный дисплей для запуска окна Avalonia на машине без экрана.
- **Docker:** 29.9.2, демон запущен, overlayfs, cgroup v2. В отличие от облачной сессии, **Docker Hub отвечает**: анонимный `docker pull` проходит (проверено на `hello-world`).
- **GitHub:** `gh` 2.103.3 аутентифицирован через OAuth (device flow), токен со скоупами `repo`, `read:org`, `gist`; `gh auth setup-git` настроен, HTTPS-remote работает по токену. Git 2.43.0, личность `r-eliseev <47721526+r-eliseev@users.noreply.github.com>`.
- **Харнес:** `scripts/check.sh` проходит.

## Что работает с оговорками
- **Локаль `C.UTF-8` и кириллические диапазоны в `grep -E`.** Под `C.UTF-8` выражение вида `[а-я]` даёт «Invalid collation character». Так раньше молча пропускалась проверка 5 в `scripts/check-docs.sh`; 2026-10-28 диапазон из скрипта убран, и проверка работает при любой локали. В новых скриптах кириллические диапазоны не использовать. `export LC_ALL=en_US.UTF-8` в `~/.bashrc` машины можно оставить.
- **Токен `gh` хранится открытым текстом** в `/root/.config/gh/hosts.yml` (режим 600): keyring-демона в контейнере нет. Это заметно для машины, которую сохраняют в образ или снимок.
- **Эмулятор Android установлен, но не запускается** — см. ниже.

## Не работает (проверено)
- **Эмулятор Android — нет.** `emulator -accel-check` отвечает `accel: 3`, «KVM requires a CPU that supports vmx or svm». Причина: VPS сам работает под KVM, но **nested virtualization не проброшен** — в `/proc/cpuinfo` нет ни `vmx`, ни `svm`, `/dev/kvm` отсутствует, `modprobe kvm_intel` даёт «Operation not supported». Эмулятор возможен только на полной программной эмуляции (TCG). Это тот же класс среды, что и облачная сессия, где замер дал «не загрузился за 31 минуту» (см. `этот раздел «Облачная сессия Claude Code»`).
- **Цель Windows у MAUI — нет.** Проверено на этой машине: без флагов — `NETSDK1100`; с `-p:EnableWindowsTargeting=true` — `NETSDK1005` (TFM не входит в `TargetFrameworks` проекта на Linux); при жёстком `-p:TargetFrameworks=net10.0-windows10.0.19041.0` флаг протекает в `ImageGen.Core` и ломает его restore. Итог совпадает с записью в `этот раздел «Облачная сессия Claude Code»`.
- **Отладка UI в эмуляторе** — по причине отсутствия KVM. Сборка, разбор APK и тесты логики — выполняются.

## Почему цель Windows не собирается на Linux (точная причина)

Проверено на VPS, 2026-10-27. Полезно, потому что старая формулировка «XAML-компилятор — инструмент под Windows» не указывает, что именно ломается.

1. `NETSDK1100` — цели Windows нельзя собирать без `-p:EnableWindowsTargeting=true`.
2. `NETSDK1005` — даже с этим флагом TFM не входит в `TargetFrameworks` проекта на Linux: в `ImageGen.Maui.csproj` он добавляется под условием `$([MSBuild]::IsOSPlatform('windows'))`. Чтобы дойти дальше, TFM надо добавить в `TargetFrameworks` вручную (тогда флаг протекает в `ImageGen.Core` и ломает его restore).
3. Дальше — собственно точка отказа:
   ```
   .../microsoft.windowsappsdk.winui/1.8.260528001/tools/net472/XamlCompiler.exe: Exec format error
   error : XamlCompiler output file "obj/Debug/net10.0-windows10.0.19041.0/win-x64/output.json" was not created.
   ```
   `XamlCompiler.exe` — это .NET Framework 4.7.2, Windows PE. Linux не может выполнить PE, отсюда «Exec format error».
4. Пакет содержит и кроссплатформенную реализацию того же компилятора: `tools/net6.0/` — только управляемые сборки (`Microsoft.UI.Xaml.Markup.Compiler.dll` и др.), без `.exe`. Но `Microsoft.UI.Xaml.Markup.Compiler.interop.targets` (строка 27) при `MSbuildRuntimeType == 'Core'` выставляет `UseXamlCompilerExecutable=true`, то есть при `dotnet build` всегда выбирается ветка с Windows `.exe`.
5. С `-p:UseXamlCompilerExecutable=false` MSBuild идёт по in-process ветке, но задача уходит out-of-process (механизм на AppDomain — только для .NET Framework), и сборка падает с `MSB4061: ... "Type must be a type provided by the runtime"`. Дополнительно нужен `Microsoft.Windows.SDK.BuildTools` — там Windows PE (`signtool.exe`, `mt.exe`, `mdmerge.exe`, `winmdidl.exe`).

Итог: цель Windows на Linux не собирается, и обойти это одним флагом нельзя.

## Wine на VPS: что он даёт и чего не даёт

Wine 9.0 установлен, префикс `/root/.wine-igp` создан. Разобрано подробно, 2026-10-27.

**Отладку Wine не даёт.** WinUI 3 требует DirectWrite, DXGI, DirectComposition и runtime Windows App SDK — Wine этого не даёт. Приложение под Wine не запустится: ни точек останова, ни Hot Reload, ни проверки интерфейса. Это ограничение не обойти.

**Сборку — да, проверено полностью.** Wine снимает первый барьер (Windows PE запускается, «Exec format error» исчезает) и позволяет дойти до конца Windows-цели: `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0` на VPS даёт **Build succeeded, 0 warnings, 0 errors** и на выходе `ImageGen.Maui.exe` (PE32+ GUI x86-64). Разобранные барьеры и обходы (проверено 2026-10-27…28):

1. **Нужен .NET Framework в префиксе.** `XamlCompiler.exe` — приложение .NET Framework 4.7.2: `err:mscoree:CLRRuntimeInfo_GetRuntimeHost: Wine Mono is not installed`. Лечится `winetricks dotnet48`, но для его установки нужен `wine32:i386` (`dpkg --add-architecture i386` + `apt-get install wine32:i386`).
2. **Нужно наполнить префикс 32-битными DLL.** `wineboot` отработал до установки `wine32`, поэтому в `syswow64` не оказалось встроенных 32-битных DLL, и 32-битные приложения падали с «Library shlwapi.dll not found». Лечится копированием недостающих builtins: `cp -n /usr/lib/i386-linux-gnu/wine/i386-windows/*.dll $WINEPREFIX/drive_c/windows/syswow64/`.
3. **Нужен MSXML6.** `makepri.exe` парсит PRI через MSXML: `fixme:msxml:SAXErrorHandler_fatalError ... "Not implemented."` → `PRI175: 0x80004001`. Встроенного в Wine msxml6 мало; помогает нативный `winetricks msxml6`.
4. **Нужен shcore!PathIsNetworkPathW.** `makepri.exe` импортирует `SHCORE.dll` по ординалу 170 (это `PathIsNetworkPathW`), а Wine реализует её как заглушку, которая завершает процесс: `wine: Call from ... to unimplemented function shcore.dll.PathIsNetworkPathW, aborting`. Обход: скопировать встроенный `shcore.dll` в префикс и **пропатчить копию** — заменить начало функции на `xor eax,eax; ret` (возвращает FALSE). Важно: патчить сам `/usr/lib/.../wine/*-windows/shcore.dll` нельзя — Wine перестаёт его грузить («Library SHCORE.dll not found»). Обход работает с `WINEDLLOVERRIDES=shcore=n` (именно `n`: с `n,b` Wine берёт из встроенной DLL заглушку `SHCreateStreamOnFileW` и makepri падает на ней).
5. **Регистр и пути.** Инструменты SDK (59 файлов `.exe` в `microsoft.windows.sdk.buildtools`) запускаются через обёртки `wine`; в аргументах абсолютные POSIX-пути переводятся в `Z:\...` (Wine отображает `Z:` на `/`), иначе Windows-инструменты считают `/foo` ключом и ругаются «Unexpected/Unknown option».
6. **Регистр MSBuild генерирует POSIX-пути**, а компилятор XAML и `makepri` ждут Windows-пути: `ItemSpec` вида `Platforms/Windows/App.xaml` схлопывается в `App.xbf` (нужны обратные слэши), а абсолютные пути в `input.json`, `output.json` и `*.resfiles` приводят к удвоению пути (`Z:\root\proj\root\proj\obj\...`). Лечится правкой этих JSON/списков перед запуском инструментов.

**Отдельно — регистр в пакетах Microsoft (обход симлинками в кэше NuGet):**
- `microsoft.windowsappsdk.winui`: targets-файл ищет `tools\Msix\...`, а пакет кладёт `tools/MSIX/` → на Linux glob не сходится, элементов 0. Нужен симлинк `tools/Msix -> tools/MSIX`.
- Там же glob `*.Msix`, а файлы называются `*.msix` → нужен симлинк на каждый файл.
- `microsoft.windowsappsdk.runtime`: PRI-карты ссылаются на `NoiseAsset_256X256_PNG.png`, а в MSIX файл называется `NoiseAsset_256x256_PNG.png` → нужен симлинк с другим регистром.

**Итог:** Windows-цель на Linux-VPS собирается, но цена — Wine с .NET Framework, MSXML6, пропатченной копией shcore, обёртками 59 SDK-инструментов и тремя симлинками в кэше NuGet. Путь рабочий, но хрупкий и неофициальный: ломается при обновлении пакетов WinAppSDK и **медленный** — каждый `makepri` под Wine работает минуты, весь шаг разворачивания PRI занимает десятки минут. Рабочий скрипт: `setup-win-target-wine.sh` на машине.

**Практический вывод.** Сборку Windows-цели уже делает CI (`.github/workflows/build.yml`, задача `maui` на `windows-latest`) — бесплатно, правильно и без хрупкости. Отладка в любом случае требует настоящей Windows-машины (`этот раздел «Windows-машина разработки»`), которой с VPS можно рулить дистанционно (SSH/WinRM/PowerShell remoting). Поэтому Wine на VPS — это способ ловить ошибки компиляции Windows-цели локально, но не способ отлаживать, и CI для компиляции лучше.

## Отладка Android: реальное устройство по сети

Рабочий путь, не требующий KVM: настоящий телефон, доступный VPS по сети.

```bash
export ANDROID_HOME=$HOME/android-sdk
export PATH=$ANDROID_HOME/platform-tools:$PATH
adb connect <ip-телефона>:5555     # на Android 11+ — «Отладка по Wi-Fi», иначе adb tcpip 5555
adb devices
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android -t:Run
```

Отладчик и Hot Reload работают по этому каналу. UI можно смотреть через `scrcpy` (по `adb`).

Проверено на этой машине: `adb` 1.0.41 / 37.0.1 установлен, сервер поднимается на `127.0.0.1:5037`. **Сам деплой на реальное устройство не проверялся** — устройства под рукой не было.

## Запуск окна Avalonia на VPS (виртуальный дисплей)

Машина без экрана, но окно оболочки запускается: нужен `Xvfb` и переменная `DISPLAY`.

```bash
apt-get install -y xvfb
Xvfb :99 -screen 0 1280x900x24 &
export DISPLAY=:99
export IMAGEGEN_DATA_DIR=~/igp-avalonia-data     # второй профиль, папка данных
dotnet run --project ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj -c Release
```

Проверено 2026-10-29: приложение стартует, создаёт БД (`imagegen.db`, схема версии 8 — миграции применяются), пишет журнал в `$IMAGEGEN_DATA_DIR/logs/imagegen-yyyyMMdd.log` первой строкой с версией, оболочкой, ОС, папками данных и логов, переходит на экран сессий и экран ключа. Без `IMAGEGEN_DATA_DIR` используется папка данных по умолчанию (`~/.local/share/ImageGen`, см. `docs/knowledge/dev-environment.md`, раздел «Логи»).

Оговорка: это запуск, а не ручная проверка интерфейса — экран не перед глазами. Глянуть на UI можно снимком (например, `import`/`xwd` из `x11-apps`), но кликать по окну по-прежнему нельзя. Задача 046 (ручная проверка Avalonia на Linux-рабочем столе) на VPS по-прежнему за человеком.

## Как поставить (при пересоздании машины)

```bash
# .NET 10 SDK
curl -sSL https://dot.net/v1/dotnet-install.sh -o ~/dotnet-install.sh
bash ~/dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet
ln -sf /usr/share/dotnet/dotnet /usr/local/bin/dotnet
export DOTNET_ROOT=/usr/share/dotnet PATH=$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH

# JDK 21
apt-get install -y openjdk-21-jdk-headless
export JAVA_HOME=/usr/lib/jvm/java-21-openjdk-amd64

# Виртуальный дисплей для запуска окна Avalonia (машина без экрана)
apt-get install -y xvfb

# Android SDK
mkdir -p ~/android-sdk/cmdline-tools && cd ~/android-sdk
curl -sSL -o clt.zip https://dl.google.com/android/repository/commandlinetools-linux-11076708_latest.zip
python3 -c "import zipfile;zipfile.ZipFile('clt.zip').extractall('cmdline-tools-tmp')"
mv cmdline-tools-tmp/cmdline-tools cmdline-tools/latest && chmod +x cmdline-tools/latest/bin/*
export ANDROID_HOME="$HOME/android-sdk"
yes | cmdline-tools/latest/bin/sdkmanager --licenses
cmdline-tools/latest/bin/sdkmanager "platform-tools" "platforms;android-36" "build-tools;36.0.0" "emulator"

# MAUI-нагрузка для Android
dotnet workload install maui-android --skip-manifest-update

# GitHub CLI и вход
apt-get install -y gh          # или официальный репозиторий cli.github.com
gh auth login --web            # device flow: код на экране, ввод на https://github.com/login/device
gh auth setup-git

# Локаль: без неё молча отключается проверка 5 в scripts/check-docs.sh
export LC_ALL=en_US.UTF-8 LANG=en_US.UTF-8
```

## Особенности среды
- **Правило «Среда» в задачах.** VPS — среда Linux, поэтому здесь выполняются задачи 001–011 (`tasks/v0.1/`). Задачи 012–016 и Windows-часть 034 — только на Windows-машине (`этот раздел «Windows-машина разработки»`). Android-часть 034 собирается, но не отлаживается (нет эмулятора).
- **Токен `gh` и SSH-ключ.** SSH-ключ сгенерирован (`~/.ssh/id_ed25519_github`, отпечаток `SHA256:7ESBH3Cxonxswz7PDptNqSS8A2rPyeSgqeVU7u+W4hw`), но не загружен в GitHub: токену не хватает скоупа `admin:public_key`. Загрузка ключа требует `gh auth refresh -h github.com -s admin:public_key`. Git при этом работает по HTTPS с токеном.
- **Docker Hub отвечает** (в отличие от облачной сессии, где были 429). Значит, образы для задачи 033 тянуть можно.
- **Окружение постоянное.** В отличие от облачной сессии, состояние переживает перезапуск агента: заново ставить SDK не нужно. В `~/.bashrc` прописаны `DOTNET_ROOT`, `ANDROID_HOME`, `JAVA_HOME` и локаль.
