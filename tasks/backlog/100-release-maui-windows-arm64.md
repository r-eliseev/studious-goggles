---
id: 100
status: planned
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
Задачу не закрывать: arm64-архив не arm64, нужен фикс workflow.

1. Способ публикации (основной): в задании `maui-windows` сузить проект до одной цели глобальным свойством и вернуть настоящий RID: `-p:TargetFrameworks=net10.0-windows10.0.19041.0 -f net10.0-windows10.0.19041.0 -r ${{ matrix.rid }}` (вместо `-p:RuntimeIdentifierOverride=…`), остальные свойства (`SelfContained`, `WindowsPackageType=None`, `WindowsAppSDKSelfContained`, версии, `-o`) без изменений. Обоснование: глобальное свойство перекрывает `TargetFrameworks` из csproj и при restore, и при publish, поэтому RID больше не применяется к Android, и `NU1102` не возникает. Комментарий над шагом переписать: почему `TargetFrameworks` сужается и что `RuntimeIdentifierOverride` игнорировался.
   - Запасной вариант, если основной не соберётся: в `ImageGen.Maui.csproj` задать `RuntimeIdentifier` только для Windows-цели из собственного свойства (например `ImageGenWindowsRid`, условие — платформа `windows` и свойство не пустое), а в workflow передавать `-p:ImageGenWindowsRid=<rid>`. Тогда Android и restore для Android RID не видят. Это правка csproj, её отметить в «Отчёте».
2. Защита от повторения: в задании `maui-windows` после публикации шаг-проверка — прочитать поле Machine PE-заголовка `publish/<rid>/ImageGen.Maui.exe` и `coreclr.dll` (смещение `e_lfanew` из байтов 0x3C, затем 2 байта после сигнатуры `PE\0\0`) и сравнить с ожидаемым: `win-x64` → 0x8664, `win-arm64` → 0xAA64; при несовпадении — ошибка задания с понятным сообщением. Средство (PowerShell или bash раннера) — по месту, без внешних утилит.
3. Проверка на ветке: запушить ветку, запустить workflow вручную (`workflow_dispatch`, «Run workflow», выбрать ветку) — релиз не создаётся, пакеты остаются артефактами запуска. Скачать артефакты `ImageGen-Maui-dev-<N>-win-x64` и `…-win-arm64` (`gh run download`), на Linux проверить `file`/`objdump -f`: в arm64 `ImageGen.Maui.exe`, `coreclr.dll`, `hostfxr.dll`, `Microsoft.WindowsAppRuntime.Bootstrap.dll`, `Microsoft.ui.xaml.dll` — Aarch64; в x64 — x86-64; размеры архивов заметно различаются. Задания Avalonia и Android должны пройти, как раньше.
4. Тест-кейсы словами для шага-проверки: x64-сборка с x64-exe → проходит; arm64-матрица с x64-exe (как в `v0.1.4`) → задание падает (проверить один раз, временно подставив неверный ожидаемый код, или убедившись по журналу первого запуска до фикса — на усмотрение исполнителя, в «Отчёте» указать как).
5. Документация: `docs/knowledge/maui.md` — неочевидный факт: при публикации многоцелевого MAUI-проекта под Windows `-p:RuntimeIdentifierOverride` игнорируется (архив собирается под архитектуру раннера), `-r` ломает restore для Android; рабочий способ — тот, что сработает в п. 1. `docs/knowledge/dev-environment.md`, раздел «Релиз» — исправить строку про `RuntimeIdentifierOverride` и добавить, что `v0.1.4` `win-arm64` фактически x64.
6. Запуск arm64-сборки на Windows on ARM в этой задаче не проверяется (нет устройства); в «Отчёте» отметить как непроверенное.

Файлы: `.github/workflows/release.yml`, `docs/knowledge/maui.md`, `docs/knowledge/dev-environment.md`; при запасном варианте — `ImageGen/ImageGen.Maui/ImageGen.Maui.csproj`.

## Отчёт

## Комментарии
### 2026-10-29, claude-plan
Вопросы владельцу:
1. Архив `ImageGen-Maui-v0.1.4-win-arm64.zip` в опубликованном релизе фактически x64. Убрать его из релиза `v0.1.4` или пометить в описании до следующего тега? Допущение плана: релиз `v0.1.4` не трогаем, исправленный пакет появится со следующим тегом.
2. Есть ли машина на Windows on ARM для ручного запуска arm64-пакета? Если нет, запуск остаётся непроверенным.
