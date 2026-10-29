# VPS: четвёртая среда разработки

Постоянная Linux-машина (VPS), четвёртая среда в репозитории. Общий обзор сред: `docs/knowledge/dev-environment.md`. Записано 2026-10-27, все пункты проверены на машине в этот день.

Отличие от остальных Linux-сред: окружение **переживает сессию** — установленные SDK и Android SDK остаются на месте, заново ставить не нужно. Рецепт ниже нужен только при пересоздании машины.

Машина: Ubuntu 24.04.5 LTS, x86_64, AMD EPYC 9355P, 4 vCPU, 15 ГБ RAM, около 185 ГБ свободно на диске. Сама работает под гипервизором KVM.

## Что работает (проверено)
- **.NET SDK 10.0.403** — та же версия, что в `docs/knowledge/claude-cloud-session.md`. Ставится в `/usr/share/dotnet` (в облачной сессии был `~/.dotnet`).
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
- **Эмулятор Android — нет.** `emulator -accel-check` отвечает `accel: 3`, «KVM requires a CPU that supports vmx or svm». Причина: VPS сам работает под KVM, но **nested virtualization не проброшен** — в `/proc/cpuinfo` нет ни `vmx`, ни `svm`, `/dev/kvm` отсутствует, `modprobe kvm_intel` даёт «Operation not supported». Эмулятор возможен только на полной программной эмуляции (TCG). Это тот же класс среды, что и облачная сессия, где замер дал «не загрузился за 31 минуту» (см. `docs/knowledge/claude-cloud-session.md`).
- **Цель Windows у MAUI — нет.** Проверено на этой машине: без флагов — `NETSDK1100`; с `-p:EnableWindowsTargeting=true` — `NETSDK1005` (TFM не входит в `TargetFrameworks` проекта на Linux); при жёстком `-p:TargetFrameworks=net10.0-windows10.0.19041.0` флаг протекает в `ImageGen.Core` и ломает его restore. Итог совпадает с записью в `docs/knowledge/claude-cloud-session.md`.
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

**Практический вывод.** Сборку Windows-цели уже делает CI (`.github/workflows/build.yml`, задача `maui` на `windows-latest`) — бесплатно, правильно и без хрупкости. Отладка в любом случае требует настоящей Windows-машины (`docs/knowledge/windows-machine.md`), которой с VPS можно рулить дистанционно (SSH/WinRM/PowerShell remoting). Поэтому Wine на VPS — это способ ловить ошибки компиляции Windows-цели локально, но не способ отлаживать, и CI для компиляции лучше.

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
- **Правило «Среда» в задачах.** VPS — среда Linux, поэтому здесь выполняются задачи 001–011 (`tasks/v0.1/`). Задачи 012–016 и Windows-часть 034 — только на Windows-машине (`docs/knowledge/windows-machine.md`). Android-часть 034 собирается, но не отлаживается (нет эмулятора).
- **Токен `gh` и SSH-ключ.** SSH-ключ сгенерирован (`~/.ssh/id_ed25519_github`, отпечаток `SHA256:7ESBH3Cxonxswz7PDptNqSS8A2rPyeSgqeVU7u+W4hw`), но не загружен в GitHub: токену не хватает скоупа `admin:public_key`. Загрузка ключа требует `gh auth refresh -h github.com -s admin:public_key`. Git при этом работает по HTTPS с токеном.
- **Docker Hub отвечает** (в отличие от облачной сессии, где были 429). Значит, образы для задачи 033 тянуть можно.
- **Окружение постоянное.** В отличие от облачной сессии, состояние переживает перезапуск агента: заново ставить SDK не нужно. В `~/.bashrc` прописаны `DOTNET_ROOT`, `ANDROID_HOME`, `JAVA_HOME` и локаль.
