---
id: 240
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-07T12:24:30Z
created: 2026-11-07
---

# UI-тесты Android: проект, фикстура с видео и smoke-сценарии

## Описание
Основной путь спецификации UI-тестов (ADR 0029, `docs/specs/ui-e2e.md`): NUnit-проект, который через Appium (UiAutomator2) запускает Release APK MAUI в эмуляторе CI, пишет видео каждого теста и при падении сохраняет скриншот и дерево UI.

Зависит от: 238 (факты: атрибут `AutomationId`, время, workflow), 239 (`TestIds`).
Среда: GitHub Actions (эмулятор); локально — сборка проекта и `dotnet format` на Linux (эмулятора в сессии нет).

## Критерии готовности
- Проект `ImageGen/ImageGen.E2E.Android/ImageGen.E2E.Android.csproj`, `net10.0`, пакеты в `ImageGen/Directory.Packages.props` (центральное управление, как остальные): `NUnit` 4.x, `NUnit3TestAdapter`, `Microsoft.NET.Test.Sdk`, `Appium.WebDriver`, `Allure.NUnit` 2.15.0. Версии `NUnit`, `NUnit3TestAdapter` и `Appium.WebDriver` выбрать последние стабильные на nuget.org в день работы и записать в «Отчёт»; `Appium.WebDriver` — та, у которой в описании пакета указана совместимость с Appium 3 (если не указано — 8.x и записать это). Ссылка на `ImageGen.E2E.Common`. Проект в `ImageGen/ImageGen.slnx`.
- Проект работает с MTP-раннером из `global.json` (`"test": {"runner": "Microsoft.Testing.Platform"}`): `NUnit3TestAdapter` 5.x поддерживает MTP через `<EnableNUnitRunner>true</EnableNUnitRunner>` и `<OutputType>Exe</OutputType>` — проверить по README пакета; если не поддерживает — остановиться и записать в «Комментарии».
- `AndroidSession` (фикстура `[SetUpFixture]` или базовый класс `AndroidTestBase`): драйвер `AndroidDriver` на `http://127.0.0.1:4723`, опции `PlatformName=Android`, `AutomationName=UiAutomator2`, `App` = полный путь из переменной `APP_APK` (нет переменной или файла → `InvalidOperationException` с текстом), `appPackage=com.eliseev.imagegen`, `noReset=false`, `newCommandTimeout=120`. Таймаут создания драйвера 3 мин.
- Перед каждым тестом: `mobile: clearApp` + `mobile: activateApp` (чистое состояние без переустановки), папка `reports/android/<Класс>.<Тест>/`, `StartRecordingScreen` с `timeLimit` 1800 с, отметка времени для logcat.
- После каждого теста (каждая операция в своём `try`, ошибка — строка в `errors.txt` папки теста): при падении `GetScreenshot` → `screenshot.png`, `PageSource` → `ui.xml`; всегда `StopRecordingScreen` → `recording.mp4`; logcat `mobile: shell` недоступен без `--relaxed-security`, поэтому logcat — через `adb logcat -d -T '<время начала>' --pid=<pid>` процессом из теста (путь `adb` — `$ANDROID_HOME/platform-tools/adb`) → `logcat.txt`; `metadata.json` (имя, статус, длительность в мс, `GITHUB_SHA`); вложения в Allure: видео `video/mp4`, скриншот `image/png`, `ui.xml` `application/xml`, logcat `text/plain` — через `AllureApi.AddAttachment`.
- Поиск элемента — один хелпер `Find(string id, TimeSpan timeout)` с ожиданием (`WebDriverWait`, 20 с по умолчанию) и локатором по факту из 238 (ожидается `MobileBy.Id(id)`); текст ошибки содержит id.
- Smoke-тесты `FirstRunTests` (`[NonParallelizable]`):
  1. `Launch_ShowsFirstRunKeyScreen` — видны `apikey.entry` и `apikey.save`.
  2. `InvalidKey_ShowsError` — ввод `sk-or-e2e-invalid`, нажатие `apikey.save`, ожидание до 30 с, пока `apikey.status` не станет непустым; текст — один из ресурсов `ProviderKeyInvalidFormat` / `ProviderKeyNetworkErrorFormat` (сравнить по началу строки до `{0}`, английская культура эмулятора); ключ не остаётся в логах теста.
  3. `Rotate_KeepsKeyScreen` — `Orientation = Landscape`, затем `Portrait`, `apikey.entry` виден.
- В `.github/workflows/ui-e2e.yml`: job `test-android` вместо `preflight-android` (preflight-скрипт оставить шагом перед тестами), `appium --log reports/android/appium.log &` с ожиданием `/status` до 120 с, `APP_APK` из артефакта, `dotnet test --project ImageGen/ImageGen.E2E.Android/ImageGen.E2E.Android.csproj -- --report-trx --results-directory reports/android` (синтаксис MTP — проверить), артефакт `android-e2e` (`reports/android/`, `if: always()`). Триггеры workflow: `workflow_dispatch`, `pull_request` (без `paths`), `push` в `main`.
- Прогон в CI зелёный, в артефакте у каждого теста своё `recording.mp4` (открыть и проверить, что видно приложение — записать в «Отчёт»).

## Анализ
Справочно: исходная спецификация владельца — `docs/specs/ui-e2e-original/` (docx и md); при расхождении действуют ADR 0029 и `docs/specs/ui-e2e.md`.
ADR 0029, `docs/specs/ui-e2e.md`, `docs/knowledge/ui-e2e.md` (из 238), `ImageGen/ImageGen.E2E.Common/TestIds.cs` (239), `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx` (тексты ошибок ключа), `ImageGen/Directory.Packages.props`, `global.json`.

## План
1. Проект, пакеты, сборка на Linux.
2. Фикстура (драйвер, папка, видео, сбор при падении) — проверка: временный тест, который падает намеренно, прогнать в CI, убедиться в `screenshot.png`/`ui.xml`; затем удалить его.
3. Три smoke-теста по одному, каждый — прогон в CI.
4. Workflow: триггеры и артефакты.

Файлы: `ImageGen/ImageGen.E2E.Android/` (новый проект: `ImageGen.E2E.Android.csproj`, `AndroidSession.cs`, `AndroidTestBase.cs`, `TestArtifacts.cs`, `FirstRunTests.cs`), `ImageGen/Directory.Packages.props`, `ImageGen/ImageGen.slnx`, `.github/workflows/ui-e2e.yml`, `docs/knowledge/ui-e2e.md`.

### Проверка
```
dotnet build ImageGen/ImageGen.E2E.Android/ImageGen.E2E.Android.csproj
dotnet format ImageGen/ImageGen.E2E.Android/ImageGen.E2E.Android.csproj --verify-no-changes
bash scripts/check.sh
# в GitHub Actions: job test-android зелёный, артефакт android-e2e с папкой на каждый тест
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
- Добавлены NUnit/MTP Android UI-тесты с Appium, фикстурами для очистки/запуска приложения, видео, logcat, metadata и вложениями Allure. Smoke-сценарии проверяют первый экран, ошибочный ключ и поворот.
- Добавлен job `test-android`: он запускает preflight и три теста на эмуляторе, сохраняет отчёты и Appium log в `android-e2e`.
- Версии NuGet выбраны по текущим стабильным пакетам: NUnit 4.6.1, NUnit3TestAdapter 6.3.1, Appium.WebDriver 9.0.0; также добавлены Allure.NUnit 2.15.0 и TRX reporter 2.5.2 для MTP.
- Проверки: `dotnet build ImageGen/ImageGen.E2E.Android/ImageGen.E2E.Android.csproj` — успешно, 0 предупреждений и ошибок; `dotnet format ... --verify-no-changes` — успешно; `bash scripts/check.sh` — успешно.
- `dotnet test ... -- --report-trx --results-directory tmp/e2e-test-results/android` обнаружил все 3 теста и сформировал TRX, но локальный запуск ожидаемо завершился ошибкой `APP_APK must point to an existing Android APK`: в среде нет собранного APK и Android-эмулятора. CI-прогон и проверка видео/артефактов ожидаются.

## Комментарии

### 2026-11-07, claude-audit
Создано по спецификации UI-тестов владельца: этап «smoke Android + видео». В исходной спецификации поиск через AccessibilityId — для MAUI на Android это SemanticProperties.Description, поэтому локатор по факту из 238. План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
Спецификация UI-тестов прислана владельцем; выбор: NUnit + Allure, сначала smoke, затем мок. План одобрен ранее данным поручением «заапрувь все таски которые будут». Изменение `.github/workflows/` по этой спецификации владелец попросил явно (ADR 0029).
