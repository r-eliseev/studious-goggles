---
id: 241
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-07T12:37:36Z
created: 2026-11-07
---

# UI-тесты Windows (экспериментально): FlaUI, видео FFmpeg, smoke-сценарии

## Описание
Windows-путь спецификации UI-тестов (ADR 0029, `docs/specs/ui-e2e.md`): NUnit-проект на FlaUI UIA3 запускает unpackaged MAUI `ImageGen.Maui.exe` на `windows-latest`, пишет видео FFmpeg каждого теста. Путь экспериментальный: если preflight 238 показал, что окна или картинки нет, задача ограничивается тем, что job честно падает с понятной причиной (а не тесты молча проходят), и записывает это.

Зависит от: 238 (факты Windows: сессия, захват, папка данных), 239, 240 (общий вид фикстуры и `TestArtifacts` — переиспользовать подход, код артефактов вынести в `ImageGen.E2E.Common`, если он не зависит от Appium).
Среда: GitHub Actions `windows-latest`; локально — сборка проекта на Linux невозможна для `net*-windows` без `EnableWindowsTargeting` — включить `<EnableWindowsTargeting>true</EnableWindowsTargeting>` в csproj, чтобы `dotnet build` и `dotnet format` шли на Linux.

## Критерии готовности
- Проект `ImageGen/ImageGen.E2E.Windows/ImageGen.E2E.Windows.csproj`, `net10.0-windows`, пакеты (центрально): `NUnit`, `NUnit3TestAdapter`, `Microsoft.NET.Test.Sdk`, `FlaUI.UIA3` 5.0.0 (FlaUI.Core подтянется), `Allure.NUnit` 2.15.0; MTP — как в 240. Ссылка на `ImageGen.E2E.Common`. В `ImageGen/ImageGen.slnx`.
- `VideoRecorder` (`IDisposable`): `Start(string path)` запускает `ffmpeg -y -f gdigrab -framerate 15 -i desktop -c:v libx264 -preset ultrafast -pix_fmt yuv420p -movflags +frag_keyframe+empty_moov <path>` с перенаправленным stdin и stderr в `ffmpeg.log`; `Stop()` пишет `q` в stdin, ждёт выхода 10 с, затем `Kill`; нет `ffmpeg` в PATH → понятное исключение. Модульный тест на сборку аргументов (`BuildArguments(path)` — чистая функция) в этом же проекте, без запуска процесса.
- `WindowsTestBase`: перед тестом — удалить папку данных MAUI (путь из факта 238; вычислять из `%LOCALAPPDATA%` так же, как записано), папка `reports/windows/<Класс>.<Тест>/`, `VideoRecorder.Start`, `Application.Launch(APP_EXE)`, главное окно через `Retry.WhileNull` 30 с. После — каждая операция в своём `try`: при падении скриншот окна (`Capture.Element(window).ToFile`) и дамп дерева (`window.FindAllDescendants()` → строки `ControlType|AutomationId|Name`) в `ui.txt`; закрыть приложение (`Close`, через 10 с `Kill`); `VideoRecorder.Stop`; копировать последний `logs/imagegen-*.log` из папки данных в `app.log`; `metadata.json`; вложения Allure.
- Smoke `FirstRunTests`: `Launch_ShowsFirstRunKeyScreen`, `InvalidKey_ShowsError` — как в 240, через `ByAutomationId`; ввод через `AsTextBox().Enter`, нажатие `AsButton().Invoke()`.
- Workflow `ui-e2e.yml`: job `test-windows` (`needs: build-windows`, `continue-on-error: true`, `timeout-minutes: 30`): `choco install ffmpeg -y --no-progress`, preflight-скрипт из 238 шагом перед тестами, `APP_EXE` = путь к `ImageGen.Maui.exe` из артефакта, `dotnet test --project ImageGen/ImageGen.E2E.Windows/...`, артефакт `windows-e2e` при любом исходе.
- В `docs/knowledge/ui-e2e.md` — итог: работают ли тесты и видео на `windows-latest`; если нет — что именно не работает (из логов). Решение снять `continue-on-error` — не в этой задаче, вопрос владельцу в `docs/status.md`, если всё зелёное 3 прогона подряд.

## Анализ
Справочно: исходная спецификация владельца — `docs/specs/ui-e2e-original/` (docx и md); при расхождении действуют ADR 0029 и `docs/specs/ui-e2e.md`.
ADR 0029, `docs/specs/ui-e2e.md`, `docs/knowledge/ui-e2e.md` (238), проект 240, FlaUI README (https://github.com/FlaUI/FlaUI): `Application.Launch`, `UIA3Automation`, `Retry.WhileNull`, `Capture.Element`.

## План
1. Проект и `VideoRecorder` с тестом аргументов (TDD, на Linux тест не запускается — `[Platform("Win")]`; прогон в CI).
2. База теста и два smoke-теста, прогон в CI.
3. Workflow и запись итога.

Файлы: `ImageGen/ImageGen.E2E.Windows/` (новый: `ImageGen.E2E.Windows.csproj`, `VideoRecorder.cs`, `VideoRecorderTests.cs`, `WindowsTestBase.cs`, `FirstRunTests.cs`), при выносе — `ImageGen/ImageGen.E2E.Common/TestArtifacts.cs`, `ImageGen/Directory.Packages.props`, `ImageGen/ImageGen.slnx`, `.github/workflows/ui-e2e.yml`, `docs/knowledge/ui-e2e.md`, `docs/status.md`.

### Проверка
```
dotnet build ImageGen/ImageGen.E2E.Windows/ImageGen.E2E.Windows.csproj
dotnet format ImageGen/ImageGen.E2E.Windows/ImageGen.E2E.Windows.csproj --verify-no-changes
bash scripts/check.sh
# в GitHub Actions: job test-windows и артефакт windows-e2e
```

### Порядок исполнения (для Luna)
- Сначала прочитай только файлы из строки «Файлы» и названные тесты; сигнатуры сверяй с текущим `main`.
- Шаги строго по порядку (ub-mode): падающий тест → запуск, убедиться, что он падает именно по описанной причине → минимальный фикс → тест зелёный.
- Не меняй файлы вне строки «Файлы», не переименовывай существующие члены сверх плана, не чини соседние находки — они в отдельных задачах.
- Если код расходится с планом (нет такого метода, другая сигнатура, тест уже есть), не импровизируй: остановись и запиши вопрос в «Комментарии».
- Команды `dotnet` выполняй по одной, не параллельно. После каждого изменённого проекта — `dotnet format <проект> --verify-no-changes`.
- В «Отчёте»: что изменено, команды и результаты, отличия от плана.

## Отчёт

### 2026-11-07, codex-gpt6 (в работе)
- Добавлены Windows NUnit/MTP UI-тесты на FlaUI UIA3 и захват FFmpeg: запуск unpackaged приложения, очистка каталога данных, ожидание окна, screenshot/UI tree при ошибке, app log, запись и metadata.
- Добавлены smoke-сценарии первого экрана и ошибочного ключа; Windows job скачивает приложение, устанавливает FFmpeg, запускает 238 preflight, тесты и загружает `windows-e2e`.
- Добавлен тест построения аргументов FFmpeg (TDD: до `VideoRecorder` сборка теста падала на отсутствующем типе).
- Проверки: `dotnet build ...ImageGen.E2E.Windows.csproj` — успешно, 0 предупреждений и ошибок; `dotnet format ... --verify-no-changes`, YAML-парсер и `bash scripts/check.sh` — успешно.
- Локальный `dotnet test` не может запустить Windows-targeted runner: Linux-сессия не содержит `Microsoft.WindowsDesktop.App`; проверка теста и приложения ожидает Windows CI. CI job добавлен с `continue-on-error: true` по плану.
- Integrated run [42374038475](https://github.com/r-eliseev/studious-goggles/actions/runs/42374038475): Windows app build and preflight passed; FlaUI launched the application and one of three tests passed. Two failures were test assertions: save button is visible but disabled before input (`IsEnabled` changed to `IsOffscreen` check), and invalid-key status did not match either expected resource (failure now prints the redacted actual status). The old relative artifact root meant per-test data was absent from upload; fixed with `E2E_REPORTS_DIR`. Rerun pending.
- CI run [42375415656](https://github.com/r-eliseev/studious-goggles/actions/runs/42375415656): first-launch and video argument tests passed; invalid-key test read the transient “Checking the key...” status immediately. It now polls until the localized invalid-format or network-error result appears. Local Windows-targeted build and formatting pass; execution awaits CI rerun.

## Комментарии

### 2026-11-07, claude-audit
Создано по спецификации UI-тестов владельца: Windows-путь, экспериментальный (в описании образа Windows 2025 нет сведений о сессии и разрешении экрана; FFmpeg не предустановлен). План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
Спецификация UI-тестов прислана владельцем; выбор: NUnit + Allure, сначала smoke, затем мок. План одобрен ранее данным поручением «заапрувь все таски которые будут». Изменение `.github/workflows/` по этой спецификации владелец попросил явно (ADR 0029).

### 2026-11-07, codex-gpt6
В run 42375415656 приложение запустилось, два теста прошли, а `InvalidKey_ShowsError` успел прочитать промежуточный статус “Checking the key...”. Изменил ожидание: тест ждёт один из конечных локализованных статусов до 30 секунд. Локальные build/format/check проходят; жду CI rerun.
