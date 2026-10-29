---
id: 089
status: approved
assignee:
claimed:
created: 2026-10-29
---

# Список сессий: название и число прогонов не обновляются

## Описание
Замечание владельца (MAUI под Windows, широкое окно, 2026-10-29): после первого сообщения в новой сессии заголовок чата сменился на текст сообщения («нарисуй квадратную синюю луну»), а в списке сессий слева строка осталась «New session · 0 runs».

Вероятная причина: `SessionsPageModel` загружает список только в `OnAppearingAsync`; в широком окне список стоит рядом с чатом (067) и заново не появляется, поэтому не видит ни нового названия (`SetTitleFromFirstMessageAsync`), ни новых прогонов, ни переименования. Проверить в «Анализе», в том числе в Avalonia (общий `SessionsPageModel`).

Зависит от: —
Среда: Linux или Windows (логика в `Presentation`, тесты на Linux; ручная проверка MAUI — 016, Avalonia — 046).

## Критерии готовности
- После первого сообщения строка сессии в списке показывает новое название без перезапуска и без перехода между экранами.
- Число прогонов в строке растёт после каждого прогона.
- Группы «Today / Earlier» и подсветка текущей сессии не ломаются; прокрутка списка и введённый поиск не сбрасываются.
- Тесты `Presentation` на обновление строки при изменении сессии; поведение одинаково в MAUI и Avalonia.

## Анализ
Причина подтверждена по коду (2026-10-29), в обеих оболочках одна и та же.

- `SessionsPageModel` (одиночка в DI обеих оболочек) строит `Sessions`, `Today`, `Earlier` только в `OnAppearingAsync`: читает `SessionService.ListAsync` и `CountRunsAsync`, пересоздаёт строки. Других путей обновления строки нет, кроме переименования из самого списка (`RenameSessionAsync` правит `item.Title`) и удаления.
- Кто вызывает `OnAppearingAsync`: MAUI — `SessionsPage.OnAppearing` и `ChatPage.OnAppearing` (список рядом с чатом); Avalonia — `SessionsView` и `ChatView.OnAppearing` (через `SessionPicker`). То есть список перечитывается, только когда открывается чат или экран списка, а не после действий в чате.
- Название меняет ядро: `AssistantService` после сохранения сообщения пользователя вызывает `SessionService.SetTitleFromFirstMessageAsync` (ставит название из первого сообщения и всякий раз сдвигает `UpdatedAt`). `ChatPageModel.ReloadAsync` потом читает ленту и показывает новый `Title` в шапке, а список об этом не знает.
- Прогоны создаёт `GenerationService` (`runs.AddRunAsync`): по сообщению ассистента и по действиям карточки (Repeat, New seed, Edit, Other model). `UpdatedAt` сессии при этом не меняется. Событий об изменении сессии в ядре нет; есть только `GenerationService.GenerationUpdated` (по генерациям, в фоновом потоке) и `ICurrentSessionTracker.Changed` (какая сессия открыта).
- `SessionItemViewModel`: `Title` и `RunCount` наблюдаемые (`DetailsText` пересчитывается при смене `RunCount`), `UpdatedText` — только для чтения, задаётся в конструкторе.
- Строки в `Today`, `Earlier`, `SearchResults` и `Sessions` — одни и те же объекты, поэтому правка строки на месте видна во всех списках сразу; прокрутка (`ScrollView` + `BindableLayout` в MAUI, аналог в Avalonia) и поиск не сбрасываются, если коллекции не пересоздавать. `OnAppearingAsync` же сбрасывает `SearchText` и пересоздаёт строки — поэтому обновлять через него нельзя.

Решение: ядро сообщает «сессия изменилась» (id), список перечитывает одну сессию и правит её строку на месте. Через ядро, а не через `ChatPageModel`, потому что изменения приходят из разных мест (ассистент в фоне, действия карточек, будущая Web-оболочка). Новый тип ядра — небольшой издатель событий-одиночка, его получают `SessionService` и `GenerationService` (необязательным последним параметром конструктора, как `IDebugTrace`, чтобы не править шесть мест создания в тестах); событие может прийти в фоновом потоке, `SessionsPageModel` переносит правку в UI-поток через `IMainThread` (как `ChatPageModel`). Это меняет устройство: `docs/architecture.md`, абзац «Обновление экрана», дополнить одной фразой.

Порядок в списке: сортировка по `UpdatedAt`, как при загрузке. Если `UpdatedAt` сессии изменился, строка переносится в начало «Today» (из «Earlier» тоже); переименование `UpdatedAt` не меняет (S1.2) и строку не двигает. Прогон без сообщения (Repeat и др.) `UpdatedAt` не меняет — строка обновляет только число прогонов и остаётся на месте. Поднимать ли сессию при новом прогоне, спецификация не говорила; решение владельца (2026-10-29): не поднимать.

Пересечения с незавершёнными задачами:
- 101 (поиск: опечатка в недописанном слове) — может менять `SessionsPageModel` (`SearchText`) и `SessionsPageModelTests`; основная правка 101 в `ImageGen.Core/Search/FuzzyMatcher.cs`. Порядок любой; если обе меняют `SessionsPageModel`, лучше 089 первой (её правка — новые члены, поиск она не трогает), при `rebase` сохранять обе стороны.
- 126 (подключение координатора удаления) меняет те же места: первичный конструктор `SessionService` (добавляет **обязательный** параметр `ISessionGenerationCoordinator`), все вызовы `new SessionService(` в тестах (`SessionServiceTests`, `AssistantServiceTests`, `ChatPageModelTests`, `SessionModelsPageModelTests`, `SessionParametersPageModelTests`, `SessionsPageModelTests`) и `CoreServiceCollectionExtensions.cs`. В C# необязательный параметр должен стоять после обязательных, поэтому правило порядка, не зависящее от того, какая задача влита первой: `SessionChangeNotifier? changes = null` всегда **последний** параметр `SessionService`; обязательный параметр координатора из 126 стоит перед ним (после `IConversationSummaryRepository summaries`). В тестах `changes` передаётся только именованным аргументом (`changes: …`), чтобы позиционная вставка координатора не ломала вызовы. При `rebase` сохранять обе стороны: и аргумент координатора, и `changes:`.
- 108, 121, 129 меняют `GenerationService.cs` в районе `CreateRunAsync` (между `runs.AddRunAsync(...)` и циклом `Launch`: 108 — аренда запуска и `RunningJob`, 121 — состояние `RunningJob`, 129 — путь запуска) и `GenerationServiceTests.cs`. Здесь в `GenerationService` только новый необязательный последний параметр конструктора и одна строка `changes?.Notify(sessionId)` сразу после журнала «Run {RunId} created…». При `rebase` сохранять обе стороны; если 108 обернула участок в аренду/блокировку, строку `Notify` ставить сразу после успешного `AddRunAsync` и журнала, до запуска генераций.
- 096 (сводка прогона) меняет `RunItemViewModel`, `GenerationItemViewModel` и их тесты, `ChatPageModelTests`; с этой задачей файлы не пересекаются (`GenerationService.cs` в строке `Файлы:` у 096 нет).
- 088 файлов этой задачи не трогает.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, членов, ключей строк и тестов переноси дословно. Если шаг не получается так, как написано (API другое, компилятор не принимает разметку), не придумывай обход: опиши в «Комментариях», что пробовал, поставь `blocked` и остановись.

Выполнять после: — (101 тоже может менять `SessionsPageModel.cs` и `SessionsPageModelTests.cs`: если 101 уже в `main`, сделать `rebase` до начала; иначе 089 первой. 126 меняет конструктор `SessionService` и все `new SessionService(` в тестах, 108/121/129 — `GenerationService.CreateRunAsync`: порядок любой, правило порядка параметров — в «Анализе», при `rebase` сохранять обе стороны.)

Решения (владелец, 2026-10-29): ядро сообщает об изменении сессии событием, список правит одну строку на месте. Прогон без сообщения (Repeat, New seed, Edit, Other model) сессию в списке не поднимает: обновляется только число прогонов. Сессия поднимается в начало «Today», только когда её `UpdatedAt` стал новее (новое сообщение). Переименование `UpdatedAt` не меняет и строку не двигает (S1.2).

Прочитай перед началом:
- этот «Анализ»;
- `ImageGen/ImageGen.Core/Services/SessionService.cs`, `ImageGen/ImageGen.Core/Services/GenerationService.cs` (метод `CreateRunAsync`), `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs`;
- `ImageGen/ImageGen.Presentation/PageModels/SessionsPageModel.cs` (`OnAppearingAsync`, `DeleteSessionAsync`, `NotifySessionsCount`), `SessionItemViewModel.cs`;
- `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionsPageModelTests.cs` (`CreateModel`, `FixedClock`, `NewSession`, `WaitUntilAsync`), `ImageGen/ImageGen.Presentation.Tests/PageModels/ChatPageModelTests.cs` (как подмена `IMainThread` выполняет действие сразу: `When(...BeginInvokeOnMainThread...).Do(ci => ci.Arg<Action>().Invoke())`);
- `docs/architecture.md`, абзац «Обновление экрана»; `docs/conventions.md` (тесты: `TestContext.Current.CancellationToken`).

1. **`SessionChangeNotifier` (ядро).** Новый файл `ImageGen/ImageGen.Core/Services/SessionChangeNotifier.cs`, пространство `ImageGen.Core.Services`: `public sealed class SessionChangeNotifier(ILogger<SessionChangeNotifier> logger)`; член `public event EventHandler<Guid>? SessionChanged;` (комментарий: может прийти в любом потоке); метод `public void Notify(Guid sessionId)`: вызывает каждый обработчик из `GetInvocationList()` по очереди с `sender = this`; исключение обработчика ловится, пишется `logger.LogWarning(exception, "Session change handler failed for {SessionId}", sessionId)`, остальные обработчики всё равно вызываются. Регистрация: `services.AddSingleton<SessionChangeNotifier>();` в `AddImageGenCore` строкой перед `services.AddSingleton<SessionService>();`.
   Тесты `ImageGen/ImageGen.Core.Tests/Services/SessionChangeNotifierTests.cs` (логгер `NullLogger<SessionChangeNotifier>.Instance`):
   - `Notify_RaisesSessionChangedWithId`: подписка, `Notify(id)` → обработчик вызван один раз с этим `id`;
   - `Notify_HandlerThrows_OtherHandlersStillCalled`: первый обработчик бросает, второй считает вызовы → `Notify` не бросает, второй вызван один раз.
2. **`SessionService` сообщает об изменениях.** В первичный конструктор самым последним параметром `SessionChangeNotifier? changes = null` (если 126 уже в `main` и там есть параметр `ISessionGenerationCoordinator` — `changes` ставится после него; координатор не трогать). После успешной записи вызвать `changes?.Notify(<id сессии>)` в `CreateAsync` (после `sessions.AddAsync`), `UpdateSettingsAsync`, `UpdateGenerationParametersAsync`, `RenameAsync`, `SetTitleFromFirstMessageAsync` (после `sessions.UpdateAsync`). Остальные места создания `SessionService` не меняются. Где `changes` передаётся (тесты), — только именованным аргументом `changes: …`.
   Тесты в `ImageGen/ImageGen.Core.Tests/Services/SessionServiceTests.cs` (сервис создаётся как в файле, плюс `changes: notifier`; подписка со списком полученных id):
   - `Create_NotifiesSessionChanged` → один id, равный id новой сессии;
   - `Rename_NotifiesSessionChanged`, `SetTitleFromFirstMessage_NotifiesSessionChanged`, `UpdateSettings_NotifiesSessionChanged`, `UpdateGenerationParameters_NotifiesSessionChanged` → один id, равный id сессии;
   - `Rename_UnknownSession_DoesNotNotify`: `GetAsync` возвращает `null` → исключение, событий нет.
3. **`GenerationService` сообщает о новом прогоне.** В первичный конструктор после `IDebugTrace? trace = null` добавить `SessionChangeNotifier? changes = null`. В `CreateRunAsync` после `runs.AddRunAsync(...)` и строки журнала «Run {RunId} created…», до цикла запуска генераций: `changes?.Notify(sessionId)`.
   Тест в `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`: `StartRun_NotifiesSessionChanged` — сервис как в файле, плюс `changes: notifier` (именованный аргумент); `StartRunAsync(_session.Id, "fox", ...)` → получен ровно один id, равный `_session.Id`. Дождаться фоновых генераций `WhenIdleAsync`, как в соседних тестах.
4. **`SessionItemViewModel.Apply`** (`ImageGen/ImageGen.Presentation/PageModels/SessionItemViewModel.cs`).
   - `UpdatedText` становится наблюдаемым: поле `[ObservableProperty] private string _updatedText;` (стиль полей, как `_title` в этом файле), задаётся в конструкторе тем же форматом (`session.UpdatedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)`); `partial void OnUpdatedTextChanged(string value) => OnPropertyChanged(nameof(DetailsText));`.
   - Новое свойство `public DateTimeOffset UpdatedAt { get; private set; }`, в конструкторе `= session.UpdatedAt`.
   - Новый метод `public void Apply(Session session, int runCount)`: `session` не `null`; если `session.Id != Id` → `ArgumentException`; присваивает `Title = session.Title`, `RunCount = runCount`, `UpdatedAt = session.UpdatedAt`, `UpdatedText` тем же форматом. `IsCurrent` и `SearchLine` не трогает.
   Тесты в `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionItemViewModelTests.cs`:
   - `Apply_NewTitle_UpdatesTitle` → `Title` новый, `PropertyChanged` для `Title` поднят;
   - `Apply_OneRun_DetailsTextShowsOneRun` → `DetailsText` оканчивается на `AppResources.RunsCountOne` («1 run»), `PropertyChanged` для `DetailsText` поднят;
   - `Apply_KeepsIsCurrentAndSearchLine` → заранее `IsCurrent = true`, `SearchLine = "x"`; после `Apply` значения те же;
   - `Apply_OtherSession_Throws` → `ArgumentException`.
5. **`SessionsPageModel` обновляет строку по событию** (`ImageGen/ImageGen.Presentation/PageModels/SessionsPageModel.cs`).
   - В первичный конструктор последними параметрами: `SessionChangeNotifier changes, IMainThread mainThread`. DI обеих оболочек их уже знает (`AddImageGenCore`, `MauiMainThread`, `AvaloniaMainThread`).
   - В начале `OnAppearingAsync`, рядом с `tracker.Changed -= …; += …`: `changes.SessionChanged -= OnSessionChanged; changes.SessionChanged += OnSessionChanged;`.
   - `private void OnSessionChanged(object? sender, Guid sessionId)` → `mainThread.BeginInvokeOnMainThread(() => RefreshSessionAsync(sessionId).FireAndForgetSafeAsync())`.
   - `private async Task RefreshSessionAsync(Guid sessionId)`, вся работа в `try`, `catch (Exception ex)` → `await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message)`:
     1. `var session = await sessionService.GetAsync(sessionId, CancellationToken.None)`;
     2. если `session` равно `null`: найти строку по `Id` в `Sessions`; если есть — удалить её из `Sessions`, `Today`, `Earlier`, `SearchResults`, вызвать `NotifySessionsCount()`; выйти;
     3. `var runCount = (await sessionService.CountRunsAsync(CancellationToken.None)).GetValueOrDefault(sessionId)`;
     4. найти строку по `Id` в `Sessions` (после всех `await`). Если строки нет: создать `new SessionItemViewModel(session, OpenSessionAsync, RenameSessionAsync, DeleteSessionAsync) { IsCurrent = session.Id == tracker.CurrentId, RunCount = runCount }`, вставить в начало `Sessions` и в начало `Today` (если `session.UpdatedAt.ToLocalTime().Date == timeProvider.GetLocalNow().Date`) или `Earlier` (иначе), вызвать `NotifySessionsCount()`; выйти;
     5. строка есть: `var moved = session.UpdatedAt > item.UpdatedAt;` затем `item.Apply(session, runCount)`; если `moved`: в `Sessions` — `Move(<индекс>, 0)`; если строка в `Earlier` — удалить оттуда; в `Today` — `Move(<индекс>, 0)`, если она там есть, иначе `Insert(0, item)`.
     6. `SearchText`, `SearchResults`, `SearchLine` не трогать, поиск не перезапускать.
   - Тесты: в `SessionsPageModelTests` поле `private readonly SessionChangeNotifier _changes = new(NullLogger<SessionChangeNotifier>.Instance);` и `private readonly IMainThread _mainThread` — подмена, выполняющая действие сразу (как в `ChatPageModelTests`). В `CreateModel` передать `changes: _changes` в `new SessionService(...)` и `_changes, _mainThread` в `new SessionsPageModel(...)`. Каждый тест: настроить `_sessionRepository.ListAsync`, `_sessionRepository.GetAsync`, `_runs.CountRunsBySessionAsync`; `await model.OnAppearingAsync()`; изменить ответы подмен; `_changes.Notify(id)`; `await WaitUntilAsync(<условие>)`.
     - `SessionChanged_NewTitle_UpdatesRowInPlace`: сессия «New session», затем `GetAsync` отдаёт её с названием «blue moon» → та же строка (`Assert.Same`) показывает `Title` «blue moon», `Sessions.Count` прежний;
     - `SessionChanged_NewRun_UpdatesRunCount`: `CountRunsBySessionAsync` сначала пустой, затем `{id: 1}` → `DetailsText` строки оканчивается на «1 run»;
     - `SessionChanged_EarlierSessionUpdatedToday_MovesToTopOfToday` (`FixedClock`): сессии A (сегодня) и B (вчера); B получает `UpdatedAt` = сейчас → `Today[0]` — B, `Earlier` не содержит B, `Sessions[0]` — B;
     - `SessionChanged_RenameWithoutNewUpdatedAt_KeepsOrder`: две сессии сегодня, вторая переименована с тем же `UpdatedAt` → порядок `Today` прежний;
     - `SessionChanged_WhileSearching_KeepsSearchTextAndResults`: `SearchText = "moon"`, дождаться результата поиска (`_searchTexts` отдаёт текст этой сессии), затем переименование и `Notify` → `SearchText` всё ещё «moon», строка в `SearchResults`, её `Title` новый;
     - `SessionChanged_CurrentSession_StaysCurrent`: `_tracker.CurrentId = id` до `Notify` → после обновления `IsCurrent` равно `true`;
     - `SessionChanged_UnknownSession_AddsRowOnTop`: `Notify` для id, которого не было в списке, `GetAsync` отдаёт сессию сегодня → `Today[0].Id` равен id, `HasNoSessions` равно `false`;
     - `SessionChanged_DeletedSession_RemovesRow`: `GetAsync` отдаёт `null` → строки нет ни в `Sessions`, ни в `Today`;
     - `SessionChanged_AfterTwoAppearings_ReadsSessionOnce`: `OnAppearingAsync` дважды, затем `Notify` → `_sessionRepository.Received(1).GetAsync(id, …)`.
6. **Оболочки.** Разметку не менять: `SessionsListView` в MAUI (`ImageGen/ImageGen.Maui/Controls/SessionsListView.xaml`) и Avalonia (`ImageGen/ImageGen.Avalonia/Views/SessionsListView.axaml`) уже привязаны к `Title` и `DetailsText`. Проверка — сборка Android, тесты Avalonia (в том числе тест композиции, который создаёт `SessionsPageModel` через DI). Если тест композиции падает из-за новых параметров — `blocked`.
7. **Документация.** `docs/architecture.md`, абзац «Обновление экрана»: дописать одно предложение — `SessionService` и `GenerationService` сообщают об изменении сессии через `SessionChangeNotifier`, и `SessionsPageModel` перечитывает одну сессию и обновляет её строку в UI-потоке. В список модуля `Services/` ядра (строка с `ApiKeyService, ModelCatalogService, SessionService, …`) добавить `SessionChangeNotifier`. Спецификацию `docs/specs/chat-sessions.md` не менять.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

### Ручная проверка
Дописать одинаковый пункт в `tasks/v0.1/016-skeleton-manual-check.md` (MAUI под Windows, широкое окно) и `tasks/backlog/046-desktop-manual-check.md` (Avalonia):
1. Новая сессия, первое сообщение → строка слева сразу показывает текст сообщения как название; после прогона — «1 run».
2. Repeat на карточке → число прогонов в строке растёт, строка не перемещается.
3. Введён поиск, список прокручен → после сообщения в чате поиск и прокрутка не сбросились, подсветка текущей сессии на месте.
В «Отчёт»: «Android (узкий экран, список на отдельной странице) — не проверено, проверить владельцу».

Файлы: `ImageGen/ImageGen.Core/Services/SessionChangeNotifier.cs` (новый), `ImageGen/ImageGen.Core/Services/SessionService.cs`, `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs`, `ImageGen/ImageGen.Core.Tests/Services/SessionChangeNotifierTests.cs` (новый), `ImageGen/ImageGen.Core.Tests/Services/SessionServiceTests.cs`, `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`, `ImageGen/ImageGen.Presentation/PageModels/SessionsPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/SessionItemViewModel.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionsPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionItemViewModelTests.cs`, `docs/architecture.md`, `tasks/v0.1/016-skeleton-manual-check.md`, `tasks/backlog/046-desktop-manual-check.md`.

## Отчёт

## Комментарии
### 2026-10-29, claude-plan
Вопрос владельцу: поднимать ли сессию наверх списка (и в «Today»), когда в ней запущен прогон без нового сообщения (Repeat, New seed, Edit, Other model)? Сейчас время изменения сессии двигает только сообщение. Допущение в плане: не поднимать, обновлять только число прогонов.
### 2026-10-29, владелец (в чате, записал claude-plan)
План одобрен; допущения из комментариев claude-plan приняты как решения. План переписан под небольшую модель.
### 2026-10-29, claude-review
Владелец попросил сделать задачу исполнимой небольшой моделью. Изменено в «Анализе» и «Плане»:
- названа 126: она добавляет обязательный параметр `ISessionGenerationCoordinator` в конструктор `SessionService` и правит те же `new SessionService(` в тестах и `CoreServiceCollectionExtensions.cs`; задано правило порядка (`changes` всегда последний, координатор перед ним, в тестах `changes:` только именованным), при `rebase` сохранять обе стороны;
- названы 108, 121, 129: меняют `GenerationService.CreateRunAsync` рядом с местом вставки `Notify`;
- убрано утверждение, что 096 может менять `GenerationService.cs`: в её строке `Файлы:` этого файла нет.
