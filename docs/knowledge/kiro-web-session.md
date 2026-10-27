# Kiro Web (браузерная сессия): среда выполнения

Что реально работает в сессии Kiro Web и как поставить недостающее. Ревизия проведена 2026-10-27 на этой машине (Amazon Linux 2023, x86_64, ядро 6.1, 8 CPU, 30 ГБ RAM, около 87 ГБ свободно на диске `/projects/sandbox`). Это третья среда разработки в дополнение к Windows-машине (`docs/knowledge/windows-machine.md`) и облачной сессии Claude Code (`docs/knowledge/claude-cloud-session.md`). Общий обзор: `docs/knowledge/dev-environment.md`. Пункты помечены: «проверено», «не работает», «не проверено».

## Устройство файловой системы (главная особенность)
- **Корень `/` и `/root` — только для чтения** (overlay `ro`). Нельзя писать в `/root/.dotnet`, `/root/.nuget`, в mise-шимы и т.п. Из-за этого `dotnet` из коробки падает с `Read-only file system : '/root/.dotnet'` и `NuGet-Migrations ... errno == EROFS`.
- **`/tmp` — tmpfs, НЕ переживает отдельную команду.** Каждый вызов терминала стартует в своём контексте: файл, записанный в `/tmp` одной командой, в следующей уже отсутствует (проверено: маркер в `/tmp` исчез, маркер в `/projects/sandbox` остался). Поэтому инструменты нельзя ставить в `/tmp`.
- **`/projects/sandbox` — rw и персистентный** (xfs, ~87 ГБ свободно), переживает команды в пределах сессии. Сюда и ставим SDK/кэши. Репозиторий лежит в `/projects/sandbox/studious-goggles`; инструменты держать рядом, вне репозитория: `/projects/sandbox/.toolcache` (не попадает в git).
- `/projects/.kiro` — конфиг Kiro (частично rw), код туда не писать.

## Предустановлено (проверено)
- **Языки через version managers:** Python (pyenv), Node 22 (nvm), Java 25 (mise), Rust, Go, Ruby, PHP и др. `git`, `curl`, `ripgrep` на месте. Сеть: `OPEN_INTERNET`.
- **.NET через mise-шим — НЕ подходит:** `dotnet --info` показывает SDK 9.0.319 и 8.0.426; это **.NET 9**, а проект таргетит `net10.0` → `NETSDK1045: does not support targeting .NET 10.0`. Ставить .NET 10 отдельно (ниже).
- **JDK 25** (`java -version` = 25.0.2) — подходит для сборки MAUI Android.

## Работает (проверено)
- **.NET SDK 10.0.403** после ручной установки в `/projects/sandbox/.toolcache/dotnet10`: `dotnet build` и `dotnet test` проходят.
  - `ImageGen.Core` — сборка успешна, тесты **59/59** зелёные.
  - `ImageGen.Data` — тесты **16/16** зелёные.
- **NuGet:** `api.nuget.org` доступен (restore проходит).
- **MAUI Android workload:** `dotnet workload install android --skip-manifest-update` ставится успешно (пишет в SDK-каталог внутри `.toolcache`, он rw). Полный `maui-android` тоже доступен (`dotnet workload search maui` показывает `maui-android`, `maui-windows`, `maui-tizen`).
- **GitHub через CLI:** `git push` и `gh api` настроены в песочнице (аутентификация автоматическая). PR создаются через `gh api repos/{owner}/{repo}/pulls` (не `gh pr create`).

## Отладка приложений
- **Windows-приложение (MAUI, цель Windows): НЕ запускается и НЕ отлаживается здесь.** Это Linux-машина; компилятор WinUI XAML и рантайм Windows — только под Windows. Как и в облачной сессии, сборка `-f net10.0-windows…` без обхода даёт `NETSDK1100`, с `-p:EnableWindowsTargeting=true` падает на XAML-компиляции. Отладка Windows-GUI (запуск `.exe`, точки останова в UI) возможна только на Windows-машине. Задачи 012–016 и Windows-часть 034 — не здесь.
- **Android-приложение: собрать APK можно, запустить/отладить вживую — нельзя (проверено частично).**
  - *Сборка:* workload `android` ставится, JDK 25 есть, Core/Data под .NET 10 собираются → сборка APK ожидаемо работает после установки Android SDK (API 36), как в облачной сессии. На момент ревизии MAUI-проекта в репозитории ещё нет (задачи 034/012 в статусе `planned`), поэтому сама APK-сборка не прогонялась — **не проверено на реальном проекте**, проверяемо только командой из облачной сессии после появления `ImageGen.Maui`.
  - *Запуск/отладка:* **эмулятора нет и запустить нельзя.** `adb`, `emulator`, `avdmanager`, `sdkmanager` в PATH отсутствуют; `ANDROID_HOME` пуст; **`/dev/kvm` отсутствует** — аппаратного ускорения нет, полноценный Android-эмулятор здесь не поднять. Физического устройства тоже нет. Значит, запуск приложения, живые точки останова, UI-инспекция, Hot Reload на Android в этой среде недоступны.
  - Итог по отладке: Kiro Web годится для **сборки и прогона модульных тестов ядра/хранилища** и для **проверки, что Android-таргет компилируется**. Интерактивная отладка GUI (и Android, и Windows) — не здесь: Android — на устройстве/эмуляторе (локально или в облачной сессии при наличии эмулятора), Windows — только на Windows-машине.

## Не работает / с оговорками (проверено)
- **Docker: не работает.** Клиент `/usr/local/bin/docker` есть, но это обёртка над podman, и запуск падает: `could not find a working conmon binary ... invalid argument`. Сборку/запуск контейнеров (задача 033) здесь провести нельзя без починки среды.
- **mise-шим `dotnet` перехватывает вызовы:** после установки своего .NET 10 обычный `dotnet` всё равно уходит в сломанный шим (`dotnet is not a valid shim`). Вызывать по абсолютному пути `/projects/sandbox/.toolcache/dotnet10/dotnet` (см. ниже), не полагаться на PATH/`export`.
- **GUI/X:** `libX11` в системе есть (ldconfig находит), но `DISPLAY` пуст и `Xvfb` не установлен — графических окон нет. Для отладки GUI это не помогает (нужен ещё рантайм платформы).

## Как поставить (нужно заново в каждой сессии: окружение не сохраняется между сессиями)
Ставим в персистентный `/projects/sandbox/.toolcache` (вне репозитория). `HOME`, `DOTNET_CLI_HOME` и `NUGET_PACKAGES` — тоже туда, иначе упрёмся в read-only `/root`.

```bash
# .NET 10 SDK в персистентный rw-каталог
mkdir -p /projects/sandbox/.toolcache && cd /projects/sandbox/.toolcache
curl -fsSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
bash dotnet-install.sh --channel 10.0 --install-dir /projects/sandbox/.toolcache/dotnet10

# Переменные для каждой команды (mise-шим dotnet ломаный — зовём по абсолютному пути)
export HOME=/projects/sandbox/.toolcache/dnhome
export DOTNET_ROOT=/projects/sandbox/.toolcache/dotnet10
export DOTNET_CLI_HOME="$HOME" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export NUGET_PACKAGES=/projects/sandbox/.toolcache/nuget
mkdir -p "$HOME" "$NUGET_PACKAGES"
DN=/projects/sandbox/.toolcache/dotnet10/dotnet

# Ядро и хранилище: сборка и тесты
$DN build ImageGen/ImageGen.Core/ImageGen.Core.csproj
$DN test  ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
$DN test  ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj

# MAUI Android workload (для проверки компиляции Android-таргета)
$DN workload install android --skip-manifest-update
# Android SDK (API 36) ставится как в облачной сессии (docs/knowledge/claude-cloud-session.md),
# но запуск эмулятора невозможен: нет /dev/kvm, adb и emulator.
```

## Чем Kiro Web отличается от облачной сессии Claude Code
- Другой базовый образ и ресурсы: Amazon Linux 2023, 8 CPU / 30 ГБ RAM / ~87 ГБ (у Claude-сессии — Ubuntu 24.04, 4 CPU / 15 ГБ / ~23 ГБ).
- **Корень read-only и эфемерный `/tmp`:** инструменты строго в `/projects/sandbox/.toolcache`, а не в `$HOME`/`/tmp`.
- **GitHub — через `git`/`gh api` CLI** (аутентификация автоматическая), а не через MCP.
- **Docker не работает** (podman/conmon сломан), тогда как в облачной сессии демон запускается вручную.
- По отладке вывод тот же: Windows-GUI — только на Windows; Android — сборка да, живой запуск нет (эмулятора/устройства нет).
