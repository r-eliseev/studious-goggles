---
id: 053
status: done
assignee:
claimed:
created: 2026-10-28
---

# Тесты падают на Windows: культура цен и блокировка временной базы

## Описание
Дефект (ревью 2026-10-28 на Windows, `main` 4d3138e). На Windows-машине владельца с культурой `pl-PL` падают два теста, которые зелёные в CI (там тесты ядра, Presentation и Avalonia идут только на `ubuntu-latest` с английской культурой):

1. `SessionModelsPageModelTests.OnAppearingAsync_AssistantListHasOnlyModelsWithToolsSortedByName` — ожидается `0.10 in, 0.50 out per 1M tokens`, получено `0,10 in, 0,50 out …`. `AssistantModelOptionViewModel.PriceText` форматирует цену `CultureInfo.CurrentCulture`, а остальные цены и сиды в интерфейсе (`GenerationItemViewModel`) — `InvariantCulture`. Интерфейс пока только английский, поэтому разделитель должен быть точкой; локализация чисел — задача 028.
2. `ChatKeyboardFlowTests.Enter_SendsInputAndAddsNoNewLine` — `IOException: The process cannot access the file 'imagegen.db'` в `TempAppPaths.Dispose` (из `ShellHarness.Dispose`). На Windows открытый файл нельзя удалить (на Linux можно), значит к моменту удаления папки базу ещё держит соединение — вероятно, отправка сообщения из теста (ход ассистента или фоновая генерация) не закончилась до `Provider.Dispose()`, или соединение живёт вне пула.

Нужно починить оба и сделать так, чтобы такие ошибки ловил CI.

Зависит от: —.
Среда: Windows для проверки пункта 2 (на Linux он не воспроизводится); пункт 1 и CI — Linux или Windows.

## Критерии готовности
- Оба теста проходят на Windows с культурой `pl-PL` и на Linux; три прогона подряд на Windows без сбоев.
- Цена модели ассистента форматируется так же, как остальные цены интерфейса (точка как разделитель), есть тест на форматирование под `pl-PL`.
- В CI тесты Core, Data, Presentation и Avalonia идут ещё и на `windows-latest` (новая задача или шаги в задаче `maui`), чтобы блокировки файлов и подобные различия ОС ловились до мерджа.
- В `docs/conventions.md` — правило: числа и цены в интерфейсе до локализации — `InvariantCulture`; тесты не зависят от культуры машины.

## Анализ
- Пункт 1: `ImageGen/ImageGen.Presentation/PageModels/AssistantModelOptionViewModel.cs`, метод `PriceText` (`string.Format(CultureInfo.CurrentCulture, …)` и `ToString("0.00##", CultureInfo.CurrentCulture)`). Пример правильного форматирования — `GenerationItemViewModel` (`CostFormat` с `InvariantCulture`).
- Пункт 2: `ImageGen/ImageGen.Avalonia.Tests/TestDoubles/ShellHarness.cs` (`Dispose`: `Provider.Dispose()`, затем `Paths.Dispose()`), `TempAppPaths.Dispose` уже вызывает `SqliteConnection.ClearAllPools()`. Значит, соединение не в пуле или ещё используется. Проверить: дожидается ли тест окончания отправки (`SendCommand` асинхронная; есть ли `GenerationService.WhenIdleAsync`), не держит ли `SqliteDatabase` соединение открытым, освобождает ли `ServiceProvider` все `IDisposable` / `IAsyncDisposable` (нужен `DisposeAsync`).
- CI: `.github/workflows/build.yml` — задачи `core` и `avalonia` на `ubuntu-latest`, `maui` на `windows-latest` (только сборка). Правка `.github/workflows/` разрешена одобрением этого плана.

## План
1. `AssistantModelOptionViewModel.PriceText` → `InvariantCulture` (как `GenerationItemViewModel`). Тест: та же модель под `CultureInfo.CurrentCulture = pl-PL` даёт `0.10 in, 0.50 out per 1M tokens`.
2. Блокировка базы: воспроизвести на Windows, найти держателя соединения (журнал Debug и `SqliteDatabase`); в `ShellHarness.Dispose` дождаться окончания работы (`await` отправки или `WhenIdleAsync`) и освободить провайдер асинхронно (`DisposeAsync`) до удаления папки. Если держит не тест, а код приложения (соединение не закрывается), — починить в `ImageGen.Data` и добавить тест в `Data.Tests`.
3. CI: шаги `dotnet test` для четырёх тестовых проектов на `windows-latest` (в задаче `maui` или отдельной `windows-tests`); культура раннера не меняется, тесты сами задают нужную культуру там, где это важно.
4. `docs/conventions.md`: правило про `InvariantCulture` для чисел и цен до локализации; `docs/knowledge/csharp.md` — факт «на Windows открытый SQLite-файл не удалить, тесты ждут окончания фоновой работы перед удалением папки».

### Команды проверки
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet format <каждый изменённый проект> --verify-no-changes
scripts/check.sh
```
На Windows с культурой `pl-PL` — три прогона Avalonia-тестов подряд.

Файлы: `ImageGen/ImageGen.Presentation/PageModels/AssistantModelOptionViewModel.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/AssistantModelOptionViewModelTests.cs` (новый или существующий), `ImageGen/ImageGen.Avalonia.Tests/TestDoubles/ShellHarness.cs`, `ImageGen/ImageGen.Avalonia.Tests/TestDoubles/TempAppPaths.cs`, `ImageGen/ImageGen.Avalonia.Tests/Flows/ChatKeyboardFlowTests.cs`, `ImageGen/ImageGen.Data/*` (если держит код приложения), `.github/workflows/build.yml`, `docs/conventions.md`, `docs/knowledge/csharp.md`

## Отчёт
Исполнитель: claude-win (Windows 11, культура `pl-PL`, SDK 10.0.402). Ветка: `task/053-windows-test-failures`.

### Что сделано
- **Цена модели ассистента** (`AssistantModelOptionViewModel.PriceText`): `InvariantCulture` вместо `CurrentCulture`, как у остальных цен интерфейса. Новый тест `AssistantModelOptionViewModelTests.DetailsText_PolishCulture_UsesPointAsDecimalSeparator` (под `pl-PL`, сначала падал).
- **Блокировка временной базы** (`ShellHarness.Dispose`): причина — фейковый ассистент отвечает вызовом `generate_images`, и генерация в фоне ещё пишет в базу и папку картинок, когда тест удаляет временную папку; на Windows открытый файл не удалить. Харнес перед удалением ждёт `GenerationService.WhenIdleAsync()`, прокачивая очередь UI-потока, с пределом 10 секунд. `GenerationService` берётся в конструкторе харнеса: `LoggingFlowTests` сам освобождает контейнер до `Dispose` (первая версия правки уронила этот тест, исправлено).
- **CI:** новая задача `windows-tests` (`windows-latest`) — сборка и тесты Core, Data, Presentation, Avalonia.
- **Документы:** `docs/conventions.md` («Тесты»: культура, ожидание фоновой работы, CI на Windows), `docs/knowledge/csharp.md` (два факта: блокировка SQLite-файла на Windows, культура машины против CI).

### Проверка
- Тесты на Windows: Core 262, Data 59, Presentation 140 из 140, Avalonia 60 из 61 (1 пропуск — интеграционный Secret Service); Avalonia — три прогона подряд без сбоев.
- `dotnet format --verify-no-changes` для `ImageGen.Presentation`, `ImageGen.Presentation.Tests`, `ImageGen.Avalonia.Tests` — без изменений; `scripts/check.sh` — пройден.
- Не проверено здесь: задача `windows-tests` в GitHub Actions — смотреть в PR.

### Отличия от плана
- Код приложения (`ImageGen.Data`) не менялся: соединение держала фоновая генерация теста, а не утечка в `SqliteDatabase`. Отдельного теста в `Data.Tests` не понадобилось.
- Культура раннера в CI не меняется; тест на `pl-PL` задаёт её сам.

## Комментарии

### 2026-10-28, ревью (claude, Windows)
Оба теста падают стабильно (два прогона подряд). Остальное зелёное: Core 262, Data 59, Presentation 138 из 139, Avalonia 59 из 61 (1 пропуск — Secret Service). MAUI под Windows и Android, Avalonia — без предупреждений.

### 2026-10-28, владелец (в чате, записал claude)
План одобрен; делать на Windows.
