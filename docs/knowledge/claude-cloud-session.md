# Облачная сессия Claude Code: среда выполнения

Что реально работает в облачной сессии (Linux-контейнер Claude Code) и как поставить недостающее. Ревизия проведена 2026-10-27 на этой машине (Ubuntu 24.04, x86_64, 4 CPU, 15 ГБ RAM, около 23 ГБ свободно на диске). Пункты помечены: «проверено», «не работает», «не проверено». Общий обзор сред: `docs/knowledge/dev-environment.md`. Windows-машина описана в `docs/knowledge/windows-machine.md`.

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
- Скрипты задач (`scripts/claim-task.sh`, `scripts/release-task.sh --publish`, `scripts/new-task.sh --publish`) пишут в ветку на origin, одноимённую текущей, поэтому пуш в `main` облачной сессии не нужен. Ветку задачи нужно опубликовать до `claim-task.sh` (`git push -u origin task/NNN-имя`), а после него сделать `git pull --ff-only`. Порядок и ограничения: `docs/tasks.md`, «Начало работы», и `docs/harness.md`, «Параллельная работа».
