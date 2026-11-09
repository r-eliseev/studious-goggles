---
id: 252
status: planned
assignee:
claimed:
created: 2026-11-09
---

# Приёмочные сценарии: Reqnroll, проект и headless Avalonia

## Описание
Просьба владельца 2026-11-09: подключить «что-то типа Cucumber» для тестов. Решение — ADR 0031 (Reqnroll на xUnit v3, headless Avalonia, русский Gherkin), каталог сценариев — `docs/specs/acceptance-scenarios.md`.

Эта задача создаёт пустой, но рабочий каркас: проект `ImageGen/ImageGen.Acceptance.Tests`, Reqnroll, запуск headless-сессии Avalonia из хуков Reqnroll и один сквозной сценарий KEY-01 («первый запуск без ключа → экран ключа»). Мир со сценарным фейком сети — задача 253.

Неизвестно (2026-11-09), выяснить первым шагом:
- работает ли `Reqnroll.xunit.v3` 3.3.4 с `xunit.v3` 4.0.2 из `Directory.Packages.props`. `Avalonia.Headless.XUnit` 12.1.4 с 4.x не работает, поэтому `ImageGen.Avalonia.Tests` закреплён на 3.2.2 (`VersionOverride`, комментарий в csproj);
- можно ли из привязок Reqnroll выполнять код на потоке Avalonia через `HeadlessUnitTestSession` (пакет `Avalonia.Headless`, без `Avalonia.Headless.XUnit`, потому что Reqnroll сам генерирует `[Fact]`, а не `[AvaloniaFact]`);
- генерирует ли Reqnroll тесты из русского Gherkin (`# language: ru`) с привязками `[Given]/[When]/[Then]` на русском тексте.

Зависит от: —
Среда: Linux или Windows (headless, без дисплея).

## Критерии готовности
- Шаг 1, пробный код в `poc/backlog/252-acceptance-reqnroll-setup/` (правила `poc/README.md`): минимальный проект с одним русским сценарием, который в хуке поднимает `HeadlessUnitTestSession` с `TestAppBuilder`-подобным `AppBuilder` и в шаге проверяет, что код выполнен на UI-потоке (`Dispatcher.UIThread.CheckAccess()`). Проверить с `xunit.v3` 4.0.2 и, если не работает, с 3.2.2. Вывод (версии, что работает, текст ошибки) — в «Комментарии» и в новый файл `docs/knowledge/acceptance-tests.md`.
  - Если не работает ни одна версия — `blocked`, вопрос владельцу: переходить ли на `Reqnroll.NUnit` (ADR 0031, п. 1).
- Проект `ImageGen/ImageGen.Acceptance.Tests` (`net10.0`, `OutputType` как у `ImageGen.Avalonia.Tests`), ссылка на `ImageGen.Avalonia`; пакеты `Reqnroll.xunit.v3`, `Avalonia.Headless` (версии в `Directory.Packages.props`), версия xunit.v3 — по итогам шага 1 с комментарием в csproj, как в `ImageGen.Avalonia.Tests`.
- `reqnroll.json`: язык фич `ru`, привязки из сборки проекта. Раскладка: `Features/`, `Steps/`, `Support/`.
- `Support/HeadlessApp` (имя — по итогам шага 1): запуск одной headless-сессии на прогон (хук `[BeforeTestRun]`), остановка в `[AfterTestRun]`, метод для выполнения асинхронного кода шага на UI-потоке с таймаутом. Папка данных — временная, как в `ImageGen.Avalonia.Tests/TestAppBuilder.cs` (настоящая папка и хранилище ключей не открываются, задача 048).
- Сценарий KEY-01 в `Features/FirstRunAndKeys.feature` зелёный. Для него хватает композиции `AppServices.Create` с заглушкой OpenRouter и подменой диалогов (как `ShellHarness`); полный мир — в 253, здесь его не делать.
- Тест-самопроверка (обычный xUnit-тест в проекте, не Gherkin): сценарий Reqnroll с несуществующим шагом падает, а не пропускается (настройка `missingOrPendingStepsOutcome` = `Error`).
- Сценарии выполняются последовательно (одна headless-сессия на процесс); параллельность xUnit для проекта выключена.
- Документация:
  - `AGENTS.md`, раздел «Команды», строка «Тесты»: добавить `dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj` (headless, на Linux);
  - `docs/conventions.md`, раздел тестов: Reqnroll только в `ImageGen.Acceptance.Tests`, русский Gherkin, теги из каталога, ссылка на ADR 0031;
  - `docs/architecture.md` — только если раскладка тестовых проектов там описана (проверить);
  - ADR 0031: дописать в «Решение» найденную версию xunit.v3 с датой;
  - `docs/index.md`: строка про `knowledge/acceptance-tests.md`.
- CI: шаг `dotnet test` проекта в job `avalonia` файла `.github/workflows/build.yml` — **только если владелец явно разрешил правку workflow** (вопрос задан в чате 2026-11-09; ответ искать в «Комментариях»). Без разрешения не трогать и записать в «Отчёт».
- `ImageGen.slnx` содержит проект.

## Анализ
- Образец композиции и headless: `ImageGen/ImageGen.Avalonia.Tests/TestAppBuilder.cs` (временная папка, `App.HostOverrides`), `ImageGen/ImageGen.Avalonia.Tests/TestDoubles/ShellHarness.cs` (`AppServices.Create`, `AvaloniaNavigationService.AttachHost`, `StartAsync`, `WaitUntilAsync`), `ImageGen/ImageGen.Avalonia.Tests/TestDoubles/TempAppPaths.cs`, `StubHttpMessageHandler.cs`, `OpenRouterFakes.cs`.
- Сценарий KEY-01 уже проверяется кодом в `ImageGen/ImageGen.Avalonia.Tests/Flows/StartFlowTests.cs` — сверить ожидания; потоковый тест не удалять (ADR 0031, п. 7).
- Двойники из `ImageGen.Avalonia.Tests` в этой задаче не переносить и не делать общими: тестовый проект на тестовый проект не ссылается. Нужное для KEY-01 — минимальная копия в `Support/` с пометкой «заменяется миром 253».
- `ImageGen.Avalonia.Tests.csproj` объясняет закрепление xunit.v3 3.2.2 — тот же риск у Reqnroll.

## План
1. POC (шаг 1), вывод в «Комментарии» и `docs/knowledge/acceptance-tests.md`; при неудаче — `blocked`.
2. Проект, пакеты, `reqnroll.json`, решение; пустой прогон `dotnet test` (0 тестов) проходит.
3. Самопроверка «неизвестный шаг = ошибка» (сначала тест, потом настройка).
4. `HeadlessApp` и хуки; KEY-01: сначала `.feature` (падает — шаги не привязаны), затем привязки.
5. Документация; CI — только с разрешения.

Файлы:
- `poc/backlog/252-acceptance-reqnroll-setup/` (новый);
- `ImageGen/ImageGen.Acceptance.Tests/` (новый): `ImageGen.Acceptance.Tests.csproj`, `reqnroll.json`, `Features/FirstRunAndKeys.feature`, `Steps/FirstRunSteps.cs`, `Support/HeadlessApp.cs`, `Support/Hooks.cs`, `ReqnrollSetupTests.cs`;
- `ImageGen/Directory.Packages.props`, `ImageGen/ImageGen.slnx`;
- `AGENTS.md`, `docs/conventions.md`, `docs/decisions/0031-bdd-acceptance-reqnroll.md`, `docs/knowledge/acceptance-tests.md` (новый), `docs/index.md`, `docs/specs/acceptance-scenarios.md` (статус KEY-01);
- при разрешении владельца: `.github/workflows/build.yml`.

### Проверка
```
dotnet build ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet format ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

### Порядок исполнения (для Luna)
- Сначала прочитай только файлы из строки «Файлы» и раздела «Анализ»; сигнатуры сверяй с текущим `main`.
- Шаг 1 (POC) обязателен до любого кода в `ImageGen/`. Версии пакетов не выдумывай: бери с nuget.org и записывай, откуда.
- Шаги строго по порядку (ub-mode): падающий тест → запуск, убедиться, что он падает именно по описанной причине → минимальный код → тест зелёный.
- Не меняй файлы вне строки «Файлы»; `ImageGen.Avalonia.Tests` не трогай; `.github/workflows/` — только с разрешения владельца.
- Если код расходится с планом, не импровизируй: остановись и запиши вопрос в «Комментарии».
- Команды `dotnet` выполняй по одной, не параллельно. После каждого изменённого проекта — `dotnet format <проект> --verify-no-changes`.
- В «Отчёте»: что изменено, версии пакетов, команды и результаты, отличия от плана.

## Отчёт

## Комментарии

### 2026-11-09, claude-bdd-plan
Создано по просьбе владельца «подключим что-то типа Cucumber» и «спланируй как таски для GPT Luna». Версии Reqnroll 3.3.4 и Allure.Reqnroll 2.15.0 проверены на nuget.org 2026-11-09; совместимость с xunit.v3 4.0.2 не проверялась — первый шаг. План ждёт одобрения владельца.
