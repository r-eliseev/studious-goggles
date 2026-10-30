---
id: 097
status: in-progress
assignee: codex-gpt6
claimed: 2026-10-30T11:18:51Z
created: 2026-10-29
---

# MAUI: лента чата не прокручивается к новым сообщениям

## Описание
Замечание владельца (2026-10-29, Android): после отправки промпта и во время генерации лента не прокручивается вниз; новые карточки оказываются ниже видимой области (на скриншоте карточка обрезана полем ввода).

Что известно: в Avalonia лента прокручивается в конец при каждом изменении `Items` (`ImageGen.Avalonia/Views/ChatView.axaml.cs`, `OnItemsChanged` → `ScrollToEnd`). В MAUI `CollectionView` ленты (`ImageGen.Maui/Pages/ChatPage.xaml`) ничего подобного не делает (`ItemsUpdatingScrollMode` не задан, `ScrollTo` не вызывается). Отдельный вопрос для «Анализа»: прокручивать ли, когда карточка внутри прогона растёт (картинка загрузилась), и не дёргать ли ленту, если пользователь сам прокрутил вверх.

Зависит от: 095, 096 (согласование клавиатуры и общих тестов чата).
Среда: Linux или Windows (Android; Windows-цель — CI и ручная проверка в 016).

## Критерии готовности
- После отправки сообщения и при появлении новых строк (ответ ассистента, прогон) лента показывает последнюю строку.
- Когда в карточке появляется картинка, нижний край последней карточки остаётся виден (если пользователь не прокрутил вверх).
- Поведение совпадает с Avalonia; при открытии сессии лента стоит в конце.

## Анализ
- **MAUI.** Лента — `CollectionView` в строке 1 сетки чата (`ChatPage.xaml`), без `x:Name`, без `ItemsUpdatingScrollMode`, без `ScrollTo`. `ChatPage.xaml.cs` на `Items` не подписан.
- **Avalonia.** `ChatView.axaml.cs`: `OnAppearing` подписывает `OnItemsChanged` на `Items.CollectionChanged`, тот откладывает `Transcript.ScrollToEnd()` до `DispatcherPriority.Background`. Прокручивает при любом изменении `Items`, не учитывает прокрутку пользователем, при обновлении карточки не прокручивает.
- **Как меняются строки** (`ChatPageModel`). `ReloadAsync` после каждого действия и при открытии очищает `Items` и строит заново. Во время генерации `OnGenerationUpdated` меняет карточку на месте (`card.Update` в UI-потоке), `Items` не трогает — для этого нужен отдельный сигнал модели (`CardUpdated`).
- **Картинка.** В текущем main зона картинки фиксирована (264×176), но 133 меняет фактические размеры карточек. Автоскролл обязан реагировать на реальный layout строки после загрузки, а не только на `CardUpdated`, которое приходит до layout. Последняя строка прогона может быть выше viewport; видимый последний индекс сам по себе не доказывает, что нижний край виден.
- **Решения (приняты владельцем).** (1) Любое изменение `Items` (открытие, отправка, «Повторить», «Новый seed», правка промпта) прокручивает в конец, даже если пользователь прокрутил вверх. (2) Обновление карточки и смена размера ленты (клавиатура, 095) прокручивают, только если лента «следует за концом». (3) Прокрутка пользователем вверх выключает следование, возврат к концу включает. (4) Правило — класс `TranscriptFollower` в `Presentation`, одно для обеих оболочек; Avalonia переводится на него.
- **Почему не `ItemsUpdatingScrollMode="KeepLastItemInView"`:** не покрывает рост карточки и прокрутку пользователем.

## План
Выполнять после: 095 (ручная проверка прокрутки при открытой клавиатуре), 096 (правит `ChatPageModelTests.cs`; конфликт только текстовый). Пересечения только текстовые: `ChatPage.xaml` — 088, 090, 098; `ChatPage.xaml.cs` — 088, 090, 098; `ChatView.axaml(.cs)` — 098. 114 и 123 добавляют тесты в `ChatPageModelTests.cs`: сохранять обе группы тестов при rebase, функциональной зависимости нет. 098 выполняется после этой задачи.

Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, членов, ключей строк и тестов переноси дословно. При расхождении с текущим API адаптируй имена и разметку в рамках контракта, исправь ошибку минимально и проверь сборкой. `blocked` нужен для неизвестного продуктового решения или недоступной обязательной среды, а не для рутинной ошибки компиляции.

Прочитай перед началом:
- `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml.cs` целиком (`OnAppearing`, `OnDisappearing`, `OnItemsChanged`);
- `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml.cs` целиком и в `ChatPage.xaml` элемент `CollectionView Grid.Row="1"` (лента);
- `ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs`: `OnAppearingAsync`, `ReloadAsync`, `OnGenerationUpdated`, `Unsubscribe`;
- `ImageGen/ImageGen.Presentation.Tests/PageModels/ChatPageModelTests.cs`: `GeneratingCard_UpdatesInPlaceWithoutReload`, `OnDisappearing_StopsUpdatingCards`, `GenerationUpdated_OtherSession_IsIgnored` (образцы настройки);
- `ImageGen/ImageGen.Avalonia.Tests/Views/SummaryMarkerTests.cs` (как добавить сообщения в сессию через `IChatMessageRepository`) и `ImageGen/ImageGen.Avalonia.Tests/TestDoubles/ShellHarness.cs` (`WaitUntilAsync`).

1. `TranscriptFollower`. Новый файл `ImageGen/ImageGen.Presentation/Utilities/TranscriptFollower.cs`, пространство `ImageGen.Presentation.Utilities`, `public sealed class TranscriptFollower`. Члены:
   - `public const double EndTolerance = 24;`
   - `public bool IsFollowing { get; private set; } = true;`
   - `public bool RowsChanged()` — ставит `IsFollowing = true`, возвращает `true`;
   - `public bool ContentChanged()` — возвращает `IsFollowing`;
   - `public bool ViewportChanged()` — возвращает `IsFollowing`;
   - `public void UserScrolled(bool atEnd)` — `IsFollowing = atEnd`;
   - `public static bool IsAtEnd(double offset, double viewport, double extent)` — `offset + viewport >= extent - EndTolerance`.
   Тесты — новый файл `ImageGen/ImageGen.Presentation.Tests/Utilities/TranscriptFollowerTests.cs` (сначала тесты, затем класс):
   - `New_IsFollowing` — новый объект → `IsFollowing == true`, `ContentChanged()` и `ViewportChanged()` возвращают `true`;
   - `UserScrolledUp_ContentChanged_DoesNotScroll` — `UserScrolled(false)` → `ContentChanged()` и `ViewportChanged()` возвращают `false`;
   - `UserScrolledUp_RowsChanged_ScrollsAndFollows` — `UserScrolled(false)` → `RowsChanged()` возвращает `true`, затем `IsFollowing == true`;
   - `UserScrolledBackToEnd_FollowsAgain` — `UserScrolled(false)`, `UserScrolled(true)` → `ContentChanged()` возвращает `true`;
   - `IsAtEnd_WithinTolerance_True` — `IsAtEnd(976, 1000, 2000)` → `true`; `IsAtEnd_AboveTolerance_False` — `IsAtEnd(900, 1000, 2000)` → `false`; `IsAtEnd_ContentShorterThanViewport_True` — `IsAtEnd(0, 1000, 500)` → `true`.
2. Событие `CardUpdated` в `ChatPageModel` (`ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs`): `public event EventHandler? CardUpdated;`. В `OnGenerationUpdated` внутри лямбды `BeginInvokeOnMainThread`, сразу после `card.Update(generation);` (в той же ветке, где карточка найдена в `_cards`), вызвать `CardUpdated?.Invoke(this, EventArgs.Empty)`. Тесты в `ChatPageModelTests.cs` (сначала тесты):
   - `CardUpdated_GenerationOfThisSession_Raised` — настройка как в `GeneratingCard_UpdatesInPlaceWithoutReload` (генератор ждёт `release`, `OnAppearingAsync`, `StartRunAsync`, ожидание обоих вызовов, `ReloadAsync`); затем подписаться счётчиком на `CardUpdated`, `release.SetResult(...)`, `await _generationService.WhenIdleAsync()` → счётчик ≥ 1;
   - `CardUpdated_OtherSession_NotRaised` — как `GenerationUpdated_OtherSession_IsIgnored`, со счётчиком `CardUpdated` → 0;
   - `CardUpdated_AfterDisappearing_NotRaised` — как `OnDisappearing_StopsUpdatingCards`, со счётчиком `CardUpdated` → 0.
3. Avalonia, падающие тесты. Новый файл `ImageGen/ImageGen.Avalonia.Tests/Views/TranscriptScrollTests.cs`, класс `TranscriptScrollTests`. Общий закрытый метод `OpenLongChatAsync(ShellHarness shell)`: ключ, база (как `SummaryMarkerTests`), новая сессия, 40 сообщений `ChatMessage` через `IChatMessageRepository.AddAsync` (роли по очереди `User`/`Assistant`, текст `"message {i}"`, `CreatedAt` по возрастанию на минуту), `shell.Window.Show()`, переход `Routes.ChatFor(session.Id)`, `WaitUntilAsync` пока `chat.Items.Count == 40`; возвращает `ChatView` и `ChatPageModel`. `ScrollViewer` ленты — `view.FindControl<ScrollViewer>("Transcript")`. «В конце» = `TranscriptFollower.IsAtEnd(t.Offset.Y, t.Viewport.Height, t.Extent.Height)`. После каждого действия — `shell.Window.UpdateLayout()` и `WaitUntilAsync`/`Dispatcher.UIThread.RunJobs()`.
   - `OpenChat_LongHistory_TranscriptAtEnd` — после открытия `WaitUntilAsync(«в конце»)` проходит, и `Offset.Y > 0`.
   - `ScrolledUp_WindowResized_StaysUp` — дождаться «в конце»; `t.Offset = new Vector(0, 0)`, `UpdateLayout`, `RunJobs`; `shell.Window.Height = 500`, `UpdateLayout`, `RunJobs` → `t.Offset.Y == 0`.
   - `ScrolledUp_Reload_ScrollsToEnd` — дождаться «в конце»; `t.Offset = new Vector(0, 0)`, `UpdateLayout`, `RunJobs`; `await chat.ReloadAsync()` → `WaitUntilAsync(«в конце»)` проходит.
   - `AtEnd_WindowShrunk_StaysAtEnd` — дождаться «в конце»; `shell.Window.Height = 400`, `UpdateLayout`, затем `WaitUntilAsync(«в конце»)` проходит.
   Новые сценарии resize/content/lifecycle должны сначала продемонстрировать дефект; проверки уже работающего открытия/Reload остаются регрессионными, их искусственно не ломать.
4. Avalonia `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml.cs`:
   - поля `private readonly TranscriptFollower _follower = new();` и `private ChatPageModel? _subscribed;`;
   - метод `private void ScrollToEndLater()` — отложенный `ScrollToEnd` с `DispatcherPriority.Background`. Добавить `_scrollPending` для объединения серии событий и `_active` для жизненного цикла. Callback проверяет `_active` и `_follower.IsFollowing`, иначе ничего; сбрасывает pending в finally. Программный scroll защищён `_programmaticScroll`, события от него не переключают follower. В callback искать текущий Transcript, не захватывать старый DataContext;
   - `OnItemsChanged` → `if (_follower.RowsChanged()) ScrollToEndLater();`;
   - `private void OnCardUpdated(object? sender, EventArgs e)` → `if (_follower.ContentChanged()) ScrollToEndLater();`;
   - `private void OnTranscriptSizeChanged(object? sender, SizeChangedEventArgs e)` → `if (_follower.ViewportChanged()) ScrollToEndLater();`;
   - `private void OnTranscriptScrollChanged(object? sender, ScrollChangedEventArgs e)` → сначала при `e.ExtentDelta.Y != 0`/`e.ViewportDelta.Y != 0` запросить scroll, только если follower уже следовал, и выйти, не классифицируя layout как жест. Если `_programmaticScroll`, `_scrollPending` или `e.OffsetDelta.Y == 0` — выйти; иначе `_follower.UserScrolled(TranscriptFollower.IsAtEnd(t.Offset.Y, t.Viewport.Height, t.Extent.Height))`, где `t` — `ScrollViewer "Transcript"`;
   - `OnAppearing`: рядом с подпиской на `Items` — отписать и подписать (`-=` затем `+=`) `model.CardUpdated`, `Transcript.ScrollChanged`, `Transcript.SizeChanged`; запомнить `_subscribed = model`, `_active = true`, сбросить follower через `RowsChanged()` и запросить начальный scroll даже для уже загруженной модели. Если `_subscribed` был другим, сначала отписаться от старого;
   - `OnDisappearing`: отписать всё то же (`CardUpdated` — у `_subscribed`), `_subscribed = null`, `_active = false`; pending callback после ухода ничего не делает. Повторное появление не удваивает подписки.
   Тесты шага 3 проходят; прогнать все `ImageGen.Avalonia.Tests`.
5. MAUI, падающий текстовый тест. Новый файл `ImageGen/ImageGen.Presentation.Tests/Design/MauiChatPageMarkupTests.cs`, класс `MauiChatPageMarkupTests` (свой `MauiRoot()`, как в `MauiStylesTests`; XML через `XDocument`, имя `x:Name` — атрибут в пространстве `http://schemas.microsoft.com/winfx/2009/xaml`). Тест `Transcript_IsNamedCollectionView`: в `Pages/ChatPage.xaml` есть элемент `CollectionView` с `x:Name="Transcript"` и `ItemsSource="{Binding Items}"`.
6. MAUI `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`: ленте (`CollectionView Grid.Row="1"` с `ItemsSource="{Binding Items}"`) добавить `x:Name="Transcript"`. Остальное не менять. Тест шага 5 проходит.
7. MAUI `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml.cs`:
   - поля `private readonly TranscriptFollower _follower = new();`, `private bool _scrollPending;`, `private bool _programmaticScroll;`, `private bool _active;`;
   - `private void ScrollToEndLater()`: если `_scrollPending` — выйти; `_scrollPending = true`; `Dispatcher.Dispatch(...)`, внутри сначала проверить `_active`, `_follower.IsFollowing`, текущий count; если строк нет, ничего. Иначе выставить `_programmaticScroll`, вызвать `Transcript.ScrollTo(_model.Items.Count - 1, position: ScrollToPosition.End, animate: false)`, затем сбросить guard после отложенного UI-dispatch; pending сбрасывать в finally. Это объединяет Reset/Add серию и не скроллит исчезнувшую страницу;
   - `private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)` → `if (_follower.RowsChanged()) ScrollToEndLater();`;
   - `private void OnCardUpdated(object? sender, EventArgs e)` → `if (_follower.ContentChanged()) ScrollToEndLater();`;
   - `private void OnTranscriptSizeChanged(object? sender, EventArgs e)` → `if (_follower.ViewportChanged()) ScrollToEndLater();`;
   - `private void OnTranscriptScrolled(object? sender, ItemsViewScrolledEventArgs e)` → если `_programmaticScroll`, `_scrollPending` или `e.VerticalDelta == 0` — выйти. Движение вверх (`VerticalDelta < 0`) всегда вызывает `UserScrolled(false)`, даже если последняя высокая строка всё ещё видна. При движении вниз (`VerticalDelta > 0`) и достижении последнего индекса — `UserScrolled(true)` и запросить позиционирование по `ScrollToPosition.End`. Последний индекс — сигнал возобновить следование, а не измерение нижнего края;
   - `OnAppearing`: `_active = true`, сбросить follower через `RowsChanged()`; до вызова `_model.OnAppearingAsync()` — отписать и подписать (`-=` затем `+=`) `_model.Items.CollectionChanged`, `_model.CardUpdated`, `Transcript.Scrolled`, `Transcript.SizeChanged`;
   - `OnDisappearing`: `_active = false`, отписать все четыре; pending callback после ухода не меняет UI.
   - В шаблонах строк ленты MAUI у корневого view сообщения/прогона добавить `SizeChanged` с одним обработчиком `OnTranscriptRowSizeChanged`: если `_active` и `ContentChanged()` → `ScrollToEndLater`. Не привязывать событие ко всему окну и не менять модели данных. Это ловит фактический рост последней строки после загрузки картинки.
   - Дополнить Avalonia headless-тесты: рост последнего run при follow → нижний край виден; тот же рост после прокрутки вверх не сдвигает viewport; исчезновение до pending callback не скроллит; повторное OnAppearing не создаёт двойных реакций.
   Сборка Android и формат (см. «Проверку»).
8. Документация:
   - `docs/specs/chat-sessions.md`, раздел «### S2. Чат и ассистент», новый пункт `S2.7` в конце раздела (после S2.6 и его продолжения): «Лента прокручивается к последней строке при открытии сессии и при каждой новой строке; пока пользователь не прокрутил вверх, лента следует и за обновлениями карточек и сменой своего размера (клавиатура) (задача 097).»
   - `docs/knowledge/maui.md`, раздел «Разметка и списки»: пункт «**Прокрутка ленты чата (задача 097):**» — `ScrollTo(индекс, ScrollToPosition.End, animate: false)` через `Dispatcher.Dispatch` после изменения `Items`; направление движения определяется `Scrolled.VerticalDelta` после фильтрации программных/pending/layout событий; последний видимый индекс — сигнал возобновления follow при движении вниз, нижний край позиционируется `ScrollToPosition.End`; на устройстве проверено или нет.
   - `docs/architecture.md` не меняется (`Utilities/` уже описан).

Завершение: заполни «Отчёт» с изменениями, командами и результатами проверок, передай ручные сценарии в 016 (Windows), 046 (Avalonia), 140 (Android) по применимости. Выполни `scripts/release-task.sh 097`, затем `bash scripts/check.sh`. Недоступная ручная проверка не заменяет сборку и не оставляет готовую задачу в `in-progress`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
bash scripts/check.sh
```
Android-сборка и формат изменённого MAUI-проекта обязательны в среде с Android SDK. Если SDK отсутствует, подготовить инструменты по `docs/knowledge/`; Windows-цель собирает CI. Устройство для ручной проверки не обязательно, её сценарии передать в 140.

### Ручная проверка
В задачу 140 (Android; результат/причину отсутствия ручной проверки записать в «Отчёт»):
1. Открыть длинную сессию → видна последняя строка.
2. Отправить промпт → видны своё сообщение, ответ ассистента и строка прогона; карточка не под полем ввода.
3. Во время генерации меняется статус карточки → нижний край последней карточки виден.
4. Прокрутить вверх во время генерации → лента не дёргается; вернуться вниз → снова следует.
5. В конце ленты открыть клавиатуру → последняя строка видна.
6. Последний прогон выше высоты экрана: прокрутить вверх внутри него (последний индекс остаётся видимым) → обновление карточек не возвращает вниз; доскроллить к концу → follow возобновляется.
7. Открыть/закрыть чат несколько раз и уйти сразу после отправки → нет повторных scroll и ошибок у исчезнувшего view.
MAUI под Windows — те же пункты 1–4 дописать в `tasks/v0.1/016-skeleton-manual-check.md`. Avalonia — пункты 1–4 дописать в `tasks/backlog/046-desktop-manual-check.md`.

Файлы: `ImageGen/ImageGen.Presentation/Utilities/TranscriptFollower.cs` (новый), `ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs`, `ImageGen/ImageGen.Presentation.Tests/Utilities/TranscriptFollowerTests.cs` (новый), `ImageGen/ImageGen.Presentation.Tests/PageModels/ChatPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/Design/MauiChatPageMarkupTests.cs` (новый), `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml.cs`, `ImageGen/ImageGen.Avalonia.Tests/Views/TranscriptScrollTests.cs` (новый), `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml.cs`, `docs/specs/chat-sessions.md`, `docs/knowledge/maui.md`, `tasks/backlog/140-*.md`, `tasks/v0.1/016-skeleton-manual-check.md`, `tasks/backlog/046-desktop-manual-check.md`.

## Отчёт

## Комментарии
### 2026-10-29, claude-plan
- Вопрос владельцу: прокручивать ли ленту в конец после «Повторить», «Новый seed» или правки промпта у старой карточки, если пользователь прокрутил вверх? Допущение в плане: да, как сейчас в Avalonia: новый прогон появляется в конце, и его нужно видеть.
- Допущение: то же правило следования (`TranscriptFollower`) ставится и в Avalonia, иначе поведение оболочек разойдётся (сейчас Avalonia не следует за ростом карточки и не учитывает прокрутку вверх).

### 2026-10-29, владелец (в чате, записал claude-plan)
План одобрен; допущения из комментариев claude-plan приняты как решения. План переписан под небольшую модель.
