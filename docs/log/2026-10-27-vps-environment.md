# 2026-10-27: VPS как четвёртая среда разработки

## Сделано
- Поднята четвёртая среда — постоянный VPS (Ubuntu 24.04.5, AMD EPYC 9355P, 4 vCPU, 15 ГБ RAM), и она описана в репозитории.
- `docs/knowledge/vps-session.md` — новая: что работает, что с оговорками, что не работает, рецепт установки, отладка Android на реальном устройстве по сети.
- `docs/knowledge/dev-environment.md` — среда добавлена в список (теперь их четыре) и в таблицу «Что где собирается» (колонка «VPS (Linux)»); дополнен раздел про строку «Среда» в задачах.
- `docs/index.md` — в перечень базы знаний добавлен `vps-session.md`.
- `docs/status.md` — в «Известные проблемы» добавлен пункт про локаль `C.UTF-8`.

## Проверка на машине
- .NET SDK 10.0.403, JDK 21.0.12, Android SDK (platforms;android-36, build-tools;36.0.0, platform-tools 37.0.1), нагрузка `maui-android`, Docker 29.9.2 с запущенным демоном.
- `dotnet format --verify-no-changes` — чисто по всем пяти проектам: Core, Core.Tests, Data, Data.Tests, Maui.
- Core.Tests 193 теста, Data.Tests 38 тестов — проходят (перепроверено на `main` от 2026-10-28, после задач 012–015; на 2026-10-27 их было 94 и 26).
- MAUI, цель Android: APK собран на проекте (`com.companyname.imagegen.maui.apk`), 0 предупреждений.
- Цель Windows: `NETSDK1100` без флагов, `NETSDK1005` с `-p:EnableWindowsTargeting=true` — подтверждено, что на Linux она не собирается.
- Эмулятор: `emulator -accel-check` → «KVM requires a CPU that supports vmx or svm» (nested virtualization не проброшен, `/dev/kvm` нет).
- `scripts/check.sh` проходит.

## Решения и причины
- Среду описать в репозитории, а не держать в переписке: по правилам `AGENTS.md` неочевидные факты о среде живут в `docs/knowledge/`, иначе следующий агент повторит замеры заново.
- Локаль лечить переменной окружения, а не правкой `scripts/`: `scripts/` не меняется без явной просьбы, и причина в локали, а не в скрипте.

## Найдено и исправлено
- **Проверка 5 в `scripts/check-docs.sh` молча пропускалась** под локалью `C.UTF-8`: `grep -E '[а-я]'` завершается с «Invalid collation character», вывод пустой, `&& fail=1` не срабатывает, ошибка уходит в stderr. Итог: харнес отчитывался об успехе, не выполнив проверку. Лечится `export LC_ALL=en_US.UTF-8` (прописано в `~/.bashrc` машины). В репозитории не менялось.

## Разобрано: почему цель Windows не собирается на Linux
- Уточнена причина, записанная в `docs/knowledge/claude-cloud-session.md` обтекуемо («XAML-компилятор — инструмент под Windows»). Последовательность: `NETSDK1100` → `NETSDK1005` (TFM добавляется в `TargetFrameworks` под условием `IsOSPlatform('windows')`) → `XamlCompiler.exe: Exec format error`.
- `XamlCompiler.exe` — .NET Framework 4.7.2, Windows PE. Пакет `microsoft.windowsappsdk.winui` содержит и кроссплатформенную реализацию (`tools/net6.0/`, только управляемые сборки), но targets-файл при `MSbuildRuntimeType == 'Core'` выставляет `UseXamlCompilerExecutable=true` и всегда берёт ветку с Windows `.exe`. С `-p:UseXamlCompilerExecutable=false` задача уходит out-of-process (механизм на AppDomain, только .NET Framework) → `MSB4061: "Type must be a type provided by the runtime"`.
- Плюс `Microsoft.Windows.SDK.BuildTools` — там Windows PE (`signtool.exe`, `mt.exe`, `mdmerge.exe`, `winmdidl.exe`).
- Вывод: цель Windows на Linux не собирается, одним флагом не обходится. В CI она собирается — `.github/workflows/build.yml`, задача `maui` на `windows-latest`.

## Wine на VPS
- Установлен Wine 9.0, префикс `/root/.wine-igp`. Первый барьер Wine снимает: Windows PE запускается, «Exec format error» исчезает.
- Отладки Wine не даёт в любом случае: WinUI 3 требует DirectWrite, DXGI, DirectComposition и runtime Windows App SDK. Под Wine приложение не запустится — ни точек останова, ни Hot Reload, ни проверки интерфейса.
- Разобрана цепочка до конца Windows-цели. Барьеры и обходы:
  1. `XamlCompiler.exe` — .NET Framework 4.7.2 → `err:mscoree:CLRRuntimeInfo_GetRuntimeHost: Wine Mono is not installed`. Нужен `winetricks dotnet48` и `wine32:i386` (иначе префикс 64-битный и .NET Framework не ставится).
  2. `makepri.exe` парсит PRI через MSXML → `fixme:msxml:SAXErrorHandler_fatalError ... "Not implemented."` → `PRI175: 0x80004001`. Лечится нативным `winetricks msxml6`.
  3. `makepri.exe` импортирует `SHCORE.dll` по ординалу 170 = `PathIsNetworkPathW` -> `wine: Call ... to unimplemented function shcore.dll.PathIsNetworkPathW, aborting`. Обход: скопировать встроенный `shcore.dll` в префикс и пропатчить копию (`xor eax,eax; ret` вместо заглушки) + `WINEDLLOVERRIDES=shcore=n`. Патчить сам builtin в `/usr/lib/.../wine/` нельзя — Wine перестаёт его грузить («Library SHCORE.dll not found»); с `shcore=n,b` Wine берёт из встроенной DLL заглушку `SHCreateStreamOnFileW` и makepri падает на ней.
  3a. `wineboot` отработал до установки `wine32`, поэтому в `syswow64` не оказалось 32-битных встроенных DLL, и 32-битные приложения падали с «Library shlwapi.dll not found». Лечится копированием недостающих builtins в префикс.
  4. 59 инструментов SDK обёрнуты в `wine`-лаунчеры; абсолютные POSIX-пути в аргументах переводятся в `Z:\...`, иначе Windows-инструменты считают `/foo` ключом.
  5. MSBuild на Linux пишет POSIX-пути, а инструменты ждут Windows-пути: `ItemSpec` `Platforms/Windows/App.xaml` схлопывался в `App.xbf` (нужны обратные слэши), абсолютные пути в `input.json`/`output.json`/`*.resfiles` давали удвоение пути. Лечится правкой этих файлов перед запуском инструментов.
- Отдельно — регистр в пакетах Microsoft, обходится симлинками в кэше NuGet: `tools\Msix\` против `tools/MSIX/`; `*.Msix` против `*.msix`; `NoiseAsset_256X256_PNG.png` в PRI против `NoiseAsset_256x256_PNG.png` в MSIX. Все три — рассчитанные на регистронезависимую ФС Windows.
- Итог проверен: `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0` на VPS даёт **Build succeeded, 0 warnings, 0 errors**, на выходе `ImageGen.Maui.exe` (PE32+ GUI x86-64). Время сборки — **1 ч 45 мин** (каждый `makepri` под Wine идёт минуты), против считанных минут в CI на `windows-latest`. Сборку Windows-цели уже делает CI (`.github/workflows/build.yml`, задача `maui` на `windows-latest`) — бесплатно и без хрупкости; отладка — на настоящей Windows-машине, которой с VPS можно рулить дистанционно (SSH/WinRM).
- Рабочий скрипт на машине: `setup-win-target-wine.sh` ( Wine, .NET Framework 4.8, MSXML6, заглушка shcore, обёртки SDK-инструментов, симлинки регистра).

## Не закончено
- SSH-ключ сгенерирован (`~/.ssh/id_ed25519_github`, отпечаток `SHA256:7ESBH3Cxonxswz7PDptNqSS8A2rPyeSgqeVU7u+W4hw`), но не загружен в GitHub: у токена `gh` нет скоупа `admin:public_key`. Загрузка требует `gh auth refresh -h github.com -s admin:public_key`. На работу не влияет: git работает по HTTPS с токеном.
- Отладка на реальном Android-устройстве по сети (`adb connect` → `dotnet build -t:Run`) описана, но не проверена: устройства под рукой не было. В описании среды помечено как непроверенное.

## На что обратить внимание
- Токен `gh` хранится открытым текстом в `/root/.config/gh/hosts.yml` (режим 600) — keyring-демона в контейнере нет. Учитывать при снимках и образах машины.
- Docker Hub на этой машине отвечает (в отличие от облачной сессии, где были 429): образы для задачи 033 тянуть можно.
- Окружение VPS постоянное, в отличие от облачной сессии: SDK и Android SDK не надо ставить заново.
