---
id: 248
status: approved
assignee:
claimed:
created: 2026-11-07
---

# UI-тесты iOS: Appium XCUITest для MAUI и Avalonia, видео и Allure

## Описание
Этап P4 дополнения Apple (ADR 0030, `docs/specs/apple-ci-e2e.md`, требования 5–6): NUnit-проект `ImageGen.E2E.iOS` гоняет одни и те же сценарии на MAUI iOS и Avalonia iOS в симуляторе через Appium XCUITest, с видео и доказательствами по контракту ADR 0029.

Зависит от: 247 (симулятор и `.app` работают), 240 (фикстура, `TestArtifacts`), 243 (мок по HTTPS — для сценария с генерацией), 239 (`TestIds`, `AutomationIdsTests`).
Среда: GitHub Actions `macos-26`; локально — сборка проекта на Linux (`net10.0`, без iOS-зависимостей) и `dotnet format`.

## Критерии готовности
- Шаг 1, локаторы (неизвестно 2026-11-07): в smoke-job 247 добавить временный шаг — Appium 3.9.0 + `appium driver install xcuitest@12.22.8` и сессия, которая сохраняет `PageSource` экрана первого запуска каждого приложения → `ui-maui.xml`, `ui-avalonia.xml`. Записать в `docs/knowledge/apple-ci.md`: в каком атрибуте (`name`, `label`, `identifier`) оказался `apikey.entry` у MAUI; что получилось у Avalonia. В Avalonia-разметке сейчас только `AutomationProperties.Name` (108), `AutomationProperties.AutomationId` нет: добавить `AutomationProperties.AutomationId` на те же элементы, что в 239 (экраны из `TestIds`, файлы в `ImageGen/ImageGen.Avalonia/Views/`), и проверить, попадает ли он в `identifier` на iOS. Если Avalonia iOS не отдаёт id в дерево accessibility — записать и искать по `name` (= `AutomationProperties.Name`, локализованный текст): такие тесты пометить как зависящие от английского интерфейса.
- `AutomationIdsTests` (239) расширить на `ImageGen/ImageGen.Avalonia/Views/*.axaml` (`AutomationProperties.AutomationId="<id>"`).
- Проект `ImageGen/ImageGen.E2E.iOS/ImageGen.E2E.iOS.csproj`: `net10.0`, NUnit, `Appium.WebDriver` (та же версия, что в 240), `Allure.NUnit` 2.15.0, ссылка на `ImageGen.E2E.Common`; MTP — как в 240. В `ImageGen/ImageGen.slnx`.
- `IosSession`: `IOSDriver`, `PlatformName=iOS`, `AutomationName=XCUITest`, `udid` из `IOS_UDID`, `app` из `APP_PATH` (`.app`), `bundleId` из `APP_BUNDLE_ID`; `APP_SHELL` = `maui` | `avalonia` (в имени папки отчёта и в метке Allure `parentSuite` = `ios-<shell>`); `newCommandTimeout` 120; `wdaLaunchTimeout` 240000 мс (первая сборка WebDriverAgent долгая).
- Перед тестом — чистые данные переустановкой (`mobile: removeApp` + `mobile: installApp` + `mobile: launchApp`; имена команд сверить с документацией драйвера 12.22.8). Для сценария с моком — до запуска `xcrun simctl get_app_container <UDID> <bundle> data`, папка данных внутри контейнера (MAUI `FileSystem.AppDataDirectory`, Avalonia — `IosAppPaths` из 246; путь записать) и копия `e2e-providers.json` как `providers.json`; корневой сертификат мока — `xcrun simctl keychain <UDID> add-root-cert <ca.pem>` один раз на job.
- Видео: `StartRecordingScreen` XCUITest; если на симуляторе не работает — свой процесс `xcrun simctl io <UDID> recordVideo --codec=h264 recording.mp4` с остановкой `SIGINT` и ожиданием 10 с. Проверить, что файл больше 10 КБ и что длительность > 0 (`ffprobe`, если есть на образе, иначе `mdls -name kMDItemDurationSeconds`); иначе — строка в `errors.txt`.
- При падении — `screenshot.png`, `ui.xml`; всегда — `simulator.log` (`log show` за время теста), `metadata.json`, вложения Allure; `appium.log` — общий файл job. Каждая операция — в своём `try`.
- Сценарии `FirstRunTests` (оба приложения): `Launch_ShowsFirstRunKeyScreen`; `InvalidKey_ShowsError` (как в 240); `Relaunch_KeepsFirstRunScreen` (`mobile: terminateApp` + `mobile: launchApp` → тот же экран, приложение не упало). После слияния 243 — `MockSessionTests.FirstRunWithMock_GeneratesImage` (как в 243) и «возврат/отмена» из дополнения: на экране сессий открыть настройки (`sessions.settings`) и вернуться назад (кнопку «Назад» найти по дереву; если нужен id — константа в `TestIds` и разметка, проверка в `AutomationIdsTests`).
- Jobs `e2e-ios-maui` и `e2e-ios-avalonia` (`macos-26`, `timeout-minutes: 60`, тесты последовательно, `workflow_dispatch` + `schedule`), Node 24, Appium с `--log reports/ios/<shell>/appium.log`, артефакты `ios-e2e-maui` / `ios-e2e-avalonia` при любом исходе.
- Повторов тестов нет. Три ночных прогона зелёные — вопрос владельцу о включении на PR в `docs/status.md`.

## Анализ
Справочно: дополнение владельца — `docs/specs/ui-e2e-original/MAUI_Avalonia_Apple_CI_E2E_Addendum.md`; при расхождении действуют ADR 0030 и `docs/specs/apple-ci-e2e.md`.
ADR 0029/0030, `docs/specs/ui-e2e.md`, `docs/specs/apple-ci-e2e.md`, проекты 240 и 243, `ImageGen/ImageGen.Avalonia/Views/` (экран ключа — имя файла сверить), документация драйвера https://appium.github.io/appium-xcuitest-driver/latest/ (capabilities и `mobile:`-команды для версии 12.22.8).

## План
1. Шаг 1 (локаторы), id в Avalonia-разметке, расширение `AutomationIdsTests`.
2. Проект и `IosSession`; временный намеренно падающий тест для проверки доказательств; затем удалить.
3. Сценарии по одному, прогон в CI на обоих приложениях.
4. Сценарии с моком после 243.

Файлы: `ImageGen/ImageGen.E2E.iOS/` (новый: csproj, `IosSession.cs`, `IosTestBase.cs`, `FirstRunTests.cs`, `MockSessionTests.cs`), `ImageGen/ImageGen.E2E.Common/TestIds.cs`, `ImageGen/ImageGen.Presentation.Tests/E2E/AutomationIdsTests.cs`, `ImageGen/ImageGen.Avalonia/Views/*.axaml` (только `AutomationProperties.AutomationId` на элементах из `TestIds`), `ImageGen/ImageGen.Maui/Pages/*.xaml` (только недостающие id), `ImageGen/Directory.Packages.props`, `ImageGen/ImageGen.slnx`, `.github/workflows/apple-ci.yml`, `docs/knowledge/apple-ci.md`, `docs/status.md`.

### Проверка
```
dotnet build ImageGen/ImageGen.E2E.iOS/ImageGen.E2E.iOS.csproj
dotnet format ImageGen/ImageGen.E2E.iOS/ImageGen.E2E.iOS.csproj --verify-no-changes
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
bash scripts/check.sh
# в GitHub Actions: e2e-ios-maui и e2e-ios-avalonia, артефакты с папкой на каждый тест
```

### Порядок исполнения (для Luna)
- Сначала прочитай только файлы из строки «Файлы» и названные тесты; сигнатуры сверяй с текущим `main`.
- Шаги строго по порядку (ub-mode): падающий тест → запуск, убедиться, что он падает именно по описанной причине → минимальный фикс → тест зелёный.
- Не меняй файлы вне строки «Файлы», не переименовывай существующие члены сверх плана, не чини соседние находки — они в отдельных задачах.
- Если код расходится с планом (нет такого метода, другая сигнатура, тест уже есть), не импровизируй: остановись и запиши вопрос в «Комментарии».
- Команды `dotnet` выполняй по одной, не параллельно. После каждого изменённого проекта — `dotnet format <проект> --verify-no-changes`.
- В «Отчёте»: что изменено, команды и результаты, отличия от плана.

## Отчёт

## Комментарии

### 2026-11-07, claude-audit
Создано по дополнению Apple: этап P4. Проверено 2026-11-07 (npm): xcuitest 13.x требует Appium 4 beta, для Appium 3 — 12.22.8. В Avalonia-разметке AutomationId нет. План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
Дополнение владельца «MAUI + Avalonia — Apple CI/E2E» (оригинал — `docs/specs/ui-e2e-original/`). План одобрен ранее данным поручением «заапрувь все таски которые будут». Новый workflow `apple-ci.yml` по этому дополнению владелец попросил явно (ADR 0030).
