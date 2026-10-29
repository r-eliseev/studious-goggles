---
id: 100
status: approved
assignee:
claimed:
created: 2026-10-29
---

# Релиз: проверить, что MAUI для Windows arm64 собран под arm64

## Описание
Замечание по релизу v0.1.4 (2026-10-29): архивы `ImageGen-Maui-v0.1.4-win-x64.zip` (109 647 779 байт) и `ImageGen-Maui-v0.1.4-win-arm64.zip` (109 647 794 байт) отличаются на 15 байт, тогда как Avalonia x64 и arm64 отличаются примерно на 2,6 МБ. Похоже, оба MAUI-архива собраны под одну архитектуру.

Что известно: `.github/workflows/release.yml`, задание `maui-windows`, публикует с `-p:RuntimeIdentifierOverride=<rid>` вместо `-r` (с `-r` restore просит Mono для Android). Доходит ли `RuntimeIdentifierOverride` до `dotnet publish` для цели `net10.0-windows10.0.19041.0` при `-o`, неизвестно. Проверить: скачать оба архива и посмотреть машину PE-заголовка `ImageGen.Maui.exe` и архитектуру нативных DLL (Windows App SDK), например `file` или `objdump -f`. Если архитектура неверна — найти рабочий способ (например, `RuntimeIdentifier` вместе с `-f`, отдельный restore для одного TFM).

Правка `.github/workflows/` — по явной просьбе владельца (эта задача и есть просьба, если план одобрен).

Зависит от: —
Среда: Linux или Windows (проверка архивов — на Linux; запуск arm64-сборки — только на Windows on ARM, если такая машина есть).

## Критерии готовности
- Выяснено и записано в «Отчёте», под какую архитектуру собраны оба архива v0.1.4.
- Если arm64-архив не arm64: workflow исправлен, пробный ручной запуск (`workflow_dispatch`) даёт архив, где `ImageGen.Maui.exe` и нативные DLL — ARM64, а x64-архив — x64.
- Неочевидный факт о публикации MAUI с RID — в `docs/knowledge/maui.md`.

## Анализ
Проверка на Linux 2026-10-29: оба архива релиза `v0.1.4` скачаны (`curl -L`, размеры 109 647 779 и 109 647 794 байт, как в «Описании»), распакованы в отдельные папки, архитектура определена `file` и `objdump -f` (поле Machine PE-заголовка). Архивы после проверки удалены.
- В обоих архивах по 652 файла с одинаковыми именами.
- `win-x64`: `ImageGen.Maui.exe`, `coreclr.dll`, `hostfxr.dll`, `Microsoft.WindowsAppRuntime.Bootstrap.dll`, `Microsoft.WindowsAppRuntime.dll`, `Microsoft.ui.xaml.dll`, `WebView2Loader.dll` — x86-64 (PE32+, `objdump`: `i386:x86-64`).
- `win-arm64`: **те же файлы тоже x86-64**, ни одного нативного ARM64-файла приложения или среды выполнения (69 нативных PE x86-64). Единственный PE с машиной Aarch64 — `Microsoft.Windows.Workloads.Resources_ec.dll` (ARM64EC-ресурсы Windows App SDK), и он лежит в обоих архивах одинаковым.
- По SHA-256 архивы различаются только `ImageGen.Maui.dll` и `ImageGen.Maui.pdb` (управляемая сборка приложения, сборка не побайтово воспроизводима); остальные 650 файлов совпадают. Отсюда разница 15 байт: в архиве `win-arm64` лежит вторая сборка под x64, а не arm64. Пакет `win-arm64` на ARM-машине в лучшем случае запустится через эмуляцию x64, нативным он не является.
- Почему `-p:RuntimeIdentifierOverride=<rid>` не сработал: по факту он проигнорирован, а RID выбран как без него — по архитектуре раннера (`windows-latest`, x64), раз `SelfContained=true`, а `RuntimeIdentifier` не задан. Читает ли какая-либо цель SDK, MAUI или Windows App SDK свойство с именем `RuntimeIdentifierOverride`, неизвестно (SDK в этой среде нет, в репозитории оно нигде не определено); проверяется журналом сборки (`-bl`, значение `RuntimeIdentifier` в задании `maui-windows`).
- Почему `-r` раньше падал: `-r` задаёт `RuntimeIdentifier` глобально, а restore многоцелевого проекта (`TargetFrameworks` в `ImageGen.Maui.csproj`: Android, iOS, Mac Catalyst, Windows на Windows-раннере) идёт по всем целевым платформам, поэтому NuGet искал `Mono.win-x64` для Android (`NU1102`, `docs/knowledge/dev-environment.md`, раздел «Релиз»). Флаг `-f` restore не сужает.
- В `docs/knowledge/dev-environment.md` (раздел «Релиз») сейчас записано, что MAUI Windows публикуется с `RuntimeIdentifierOverride`, и что `win-x64`/`win-arm64` собираются: второе верно лишь в том, что сборка проходит.
- Пересечения: `.github/workflows/release.yml` меняет и задача 099 (задание `publish`), эта — задание `maui-windows`. Порядок любой; вторая делает `git rebase origin/main`.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов и проверяемые контракты сохраняй. Рутинные несовпадения PowerShell/MSBuild API уточняй по текущему SDK и исправляй в пределах выбранного варианта. `blocked` — только при реальном препятствии (нет доступа к Actions либо оба варианта публикации не работают), с командами и журналами; не переключайся на смену архитектуры пакета или отключение проверки.

Выполнять после: нет обязательных. 099 меняет тот же `.github/workflows/release.yml`, но другое задание (`publish`), и другой абзац раздела «Релиз» в `docs/knowledge/dev-environment.md`; функциональной зависимости нет. При `rebase` сохранить обе правки. В `docs/knowledge/maui.md` пишут и 088, 090, 095, 097, 098: только добавляй свой пункт, при конфликте при `rebase` сохраняй обе версии.

Правка `.github/workflows/release.yml` (только задание `maui-windows`) входит в эту задачу по её «Описанию» и разрешена после одобрения плана (статус `approved`). Другие задания и файлы в `.github/` и `scripts/` не трогать.

Решения по вопросам из «Комментариев» (переданы основным агентом 2026-10-29; одобрение плана подтверждает владелец):
- Задачу не закрывать «как есть»: архив `win-arm64` в `v0.1.4` — x64 («Анализ»), workflow исправляется.
- Архив `ImageGen-Maui-v0.1.4-win-arm64.zip` из релиза `v0.1.4` удаляет владелец вручную (см. «Ручная проверка»); исполнитель релизы не меняет.
- Устройства Windows on ARM нет: запуск arm64-пакета помечается «не проверено».
- Тестов для workflow нет: «падающий тест» — шаг-проверка архитектуры в workflow, который сначала падает на `win-arm64` (шаги 1–2).

Прочитай перед началом: `.github/workflows/release.yml` (задание `maui-windows`), `ImageGen/ImageGen.Maui/ImageGen.Maui.csproj` (`TargetFrameworks`), `docs/knowledge/dev-environment.md` (раздел «Релиз»), `docs/knowledge/maui.md`, `docs/tasks.md` («Завершение»), «Анализ» выше.

Нужно: `gh` с доступом к репозиторию (запуск workflow и скачивание артефактов), ветка задачи запушена в `origin`. Если `gh workflow run` отвечает отказом в доступе — `blocked`, в «Комментариях» вывод команды.

1. **Падающая проверка: шаг «Check PE machine».** В `.github/workflows/release.yml`, задание `maui-windows`, между шагами «Publish MAUI Windows (unpackaged, self-contained)» и «Pack» добавить шаг с именем `Check PE machine`, `shell: pwsh`. Что делает (без внешних утилит, только .NET из PowerShell):
   - ожидаемый код машины по `${{ matrix.rid }}`: `win-x64` → `0x8664`, `win-arm64` → `0xAA64`;
   - для каждого из файлов `publish/<rid>/ImageGen.Maui.exe`, `publish/<rid>/coreclr.dll`, `publish/<rid>/Microsoft.WindowsAppRuntime.Bootstrap.dll`: прочитать байты (`[System.IO.File]::ReadAllBytes`), проверить существование и минимальную длину файла/`MZ`, затем смещение PE-заголовка — `Int32` по адресу `0x3C` (`[BitConverter]::ToInt32`); проверить границы смещения и сигнатуру `PE` с двумя нулевыми байтами, поле Machine — `UInt16` по адресу «смещение + 4» (`[BitConverter]::ToUInt16`). Отсутствующий, короткий или неверный PE тоже означает ошибку проверки, а не пропуск;
   - печатать строку `<файл>: 0x<код в hex>`; если код не равен ожидаемому — накопить ошибку и напечатать «<файл>: machine 0x<код>, expected 0x<ожидаемый> for <rid>». После всех трёх файлов при любой ошибке `exit 1`. Не использовать прекращающий выполнение `Write-Error` внутри цикла: у `pwsh` в Actions `ErrorActionPreference=Stop`, он не даст проверить остальные файлы.
   - Комментарий над шагом по-английски: «Fail if the published binaries are not built for the matrix RID (v0.1.4 shipped x64 binaries as win-arm64).»
   - Закоммить, запушь ветку, запусти `gh workflow run release.yml --ref <ветка>`, найди запуск (`gh run list --workflow release.yml --branch <ветка> -L 1`) и дождись (`gh run watch <id>`).
   - Ожидается: задание `maui-windows (win-x64)` зелёное, `maui-windows (win-arm64)` падает на «Check PE machine» с `0x8664, expected 0xAA64`. Запиши в «Отчёт» номер запуска и строки шага.
   - Если `win-arm64` уже прошёл (код `0xAA64`) — проверить SHA исходников/параметры публикации запуска и артефакты шага 3: возможно, исправление уже попало в `main`. Сохранить проверку архитектуры, не менять рабочую публикацию без необходимости; в «Отчёте» дать доказательства. Если `win-x64` упал — сначала различить ошибку PE-reader и действительно неверный артефакт, затем исправить соответствующую причину и повторить запуск. Несовпадение с историческим ожиданием само по себе не требует `blocked`.
2. **Исправление публикации.** В шаге «Publish MAUI Windows (unpackaged, self-contained)» заменить `-p:RuntimeIdentifierOverride=${{ matrix.rid }}` на `-p:TargetFrameworks=net10.0-windows10.0.19041.0 -r ${{ matrix.rid }}`; `-f net10.0-windows10.0.19041.0` и все остальные свойства (`-p:SelfContained=true`, `-p:WindowsPackageType=None`, `-p:WindowsAppSDKSelfContained=true`, версии, `-o publish/${{ matrix.rid }}`) оставить. Комментарий над шагом заменить на (по-английски): «Global TargetFrameworks narrows restore and publish to Windows, so -r does not reach Android (NU1102 Mono.win-x64). RuntimeIdentifierOverride was ignored: win-arm64 got x64 binaries.»
   - Обоснование: глобальное свойство `TargetFrameworks` перекрывает значение из `ImageGen.Maui.csproj` и при restore, и при publish, поэтому проект становится одноцелевым, а `-r` больше не применяется к Android.
   - Закоммить, запушь, запусти workflow так же, как в шаге 1.
   - Если оба задания `maui-windows` зелёные → шаг 3.
   - Если падает restore или publish (например, `NU1102`, `NETSDK…`) → шаг 2а.
   - Если publish проходит, но «Check PE machine» на `win-arm64` по-прежнему `0x8664` → шаг 2а.
2а. **Запасной вариант (только если шаг 2 не прошёл).** Вернуть в шаге публикации исходную строку без `-p:TargetFrameworks` и без `-r`, вместо `-p:RuntimeIdentifierOverride=…` передать `-p:ImageGenWindowsRid=${{ matrix.rid }}`. В `ImageGen/ImageGen.Maui/ImageGen.Maui.csproj`, в первой `PropertyGroup` рядом со строками `SupportedOSPlatformVersion`, добавить `RuntimeIdentifier` со значением `$(ImageGenWindowsRid)` и условием: платформа цели (`GetTargetPlatformIdentifier('$(TargetFramework)')`) равна `windows` и `'$(ImageGenWindowsRid)' != ''`. Комментарий над шагом workflow — про `ImageGenWindowsRid` вместо текста из шага 2. Запусти workflow. Если оба задания `maui-windows` зелёные → шаг 3; иначе — `blocked`, в «Комментариях» оба варианта и ошибки из журналов.
3. **Проверка артефактов на Linux.** `gh run download <id> -n ImageGen-Maui-dev-<N>-win-x64 -D tmp/rel100/x64` и `-n ImageGen-Maui-dev-<N>-win-arm64 -D tmp/rel100/arm64` (`<N>` — номер запуска: `gh run view <id> --json number`). Каждый zip распаковать в отдельную пустую папку внутри `tmp/rel100/`. `file` для `ImageGen.Maui.exe`, `coreclr.dll`, `hostfxr.dll`, `Microsoft.WindowsAppRuntime.Bootstrap.dll`, `Microsoft.ui.xaml.dll`: в `x64` — `x86-64`, в `arm64` — `Aarch64`. Результаты PE Machine — в «Отчёт». Размеры zip можно записать как диагностическую справку: различие размеров не доказывает правильную архитектуру и не входит в pass/fail. Если хоть один из перечисленных файлов не той архитектуры — вернуться к диагностике RID из шага 2/2а; не принимать пакет по имени или размеру. Затем удали только созданный здесь `tmp/rel100`.
4. Задания `avalonia` (6 штук) и `maui-android` в тех же запусках должны быть зелёными. При падении сравнить журнал и SHA с актуальным `main`: вызванную своей правкой регрессию исправить; независимый инфраструктурный сбой повторить один раз и явно записать ограничение проверки. Не объявлять успех проверки, пока соответствующая сборка не подтверждена.
5. **Документация.**
   - `docs/knowledge/maui.md`: новый пункт «Публикация MAUI под Windows с RID»: в многоцелевом проекте `-r` с `-f` ломает restore для Android (`NU1102 Mono.win-x64`); `-p:RuntimeIdentifierOverride` игнорируется — пакет собирается под архитектуру машины сборки (в `v0.1.4` `win-arm64` содержал x64); рабочий способ — тот, что прошёл (шаг 2 или 2а), с датой 2026-10-29 и номером запуска.
   - `docs/knowledge/dev-environment.md`, раздел «Релиз»: строку «MAUI Windows публикуется с `-p:RuntimeIdentifierOverride=<rid>`…» заменить на рабочий способ со ссылкой на `docs/knowledge/maui.md`; добавить, что архив `win-arm64` в `v0.1.4` фактически x64 и что задание проверяет поле Machine PE-заголовка. Строку «Пакеты под arm64 и macOS на устройствах не запускались» оставить.

6. Заполни «Отчёт»: подтверждённая причина, выбранный вариант, SHA/номера запусков, таблица RID → файл → Machine, непроверенный запуск на Windows ARM. Выполни `scripts/release-task.sh 100`, затем `bash scripts/check.sh`, актуализируй ветку от `origin/main` и подготовь PR по `docs/tasks.md`.

### Проверка
```
gh run list --workflow release.yml --branch <ветка> -L 3
bash scripts/check.sh
```
плюс проверка артефактов из шага 3. При варианте 2а дополнительно `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android` (если в среде есть Android SDK; иначе — по зелёному `maui-android` в запуске). C#-код не меняется: `dotnet test` и `dotnet format` не нужны.

### Ручная проверка
В «Отчёт» (для владельца):
- «Удалить из релиза `v0.1.4` неверный архив: `gh release delete-asset v0.1.4 ImageGen-Maui-v0.1.4-win-arm64.zip` (или в GitHub → Releases → v0.1.4 → удалить файл). Исполнитель этого не делает.»
- «Запуск MAUI `win-arm64` на Windows on ARM: не проверено (устройства нет).»
- «Следующий тег: у файлов приложения и среды выполнения внутри каждого MAUI-архива поле PE Machine соответствует указанному RID; размер zip не является критерием.»

Файлы: `.github/workflows/release.yml`, `docs/knowledge/maui.md`, `docs/knowledge/dev-environment.md`; только при варианте 2а — `ImageGen/ImageGen.Maui/ImageGen.Maui.csproj`.

## Отчёт

## Комментарии
### 2026-10-29, claude-plan
Вопросы владельцу:
1. Архив `ImageGen-Maui-v0.1.4-win-arm64.zip` в опубликованном релизе фактически x64. Убрать его из релиза `v0.1.4` или пометить в описании до следующего тега? Допущение плана: релиз `v0.1.4` не трогаем, исправленный пакет появится со следующим тегом.
2. Есть ли машина на Windows on ARM для ручного запуска arm64-пакета? Если нет, запуск остаётся непроверенным.

### 2026-10-29, владелец (в чате, записал claude-plan)
План одобрен; допущения из комментариев claude-plan приняты как решения. План переписан под небольшую модель.
