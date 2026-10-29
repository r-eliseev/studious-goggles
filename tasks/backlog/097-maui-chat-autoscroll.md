---
id: 097
status: approved
assignee:
claimed:
created: 2026-10-29
---

# MAUI: лента чата не прокручивается к новым сообщениям

## Описание
Замечание владельца (2026-10-29, Android): после отправки промпта и во время генерации лента не прокручивается вниз; новые карточки оказываются ниже видимой области (на скриншоте карточка обрезана полем ввода).

Что известно: в Avalonia лента прокручивается в конец при каждом изменении `Items` (`ImageGen.Avalonia/Views/ChatView.axaml.cs`, `OnItemsChanged` → `ScrollToEnd`). В MAUI `CollectionView` ленты (`ImageGen.Maui/Pages/ChatPage.xaml`) ничего подобного не делает (`ItemsUpdatingScrollMode` не задан, `ScrollTo` не вызывается). Отдельный вопрос для «Анализа»: прокручивать ли, когда карточка внутри прогона растёт (картинка загрузилась), и не дёргать ли ленту, если пользователь сам прокрутил вверх.

Зависит от: —
Среда: Linux или Windows (Android; Windows-цель — CI и ручная проверка в 016).

## Критерии готовности
- После отправки сообщения и при появлении новых строк (ответ ассистента, прогон) лента показывает последнюю строку.
- Когда в карточке появляется картинка, нижний край последней карточки остаётся виден (если пользователь не прокрутил вверх).
- Поведение совпадает с Avalonia; при открытии сессии лента стоит в конце.

## Анализ
- **MAUI.** Лента — `CollectionView` в строке 1 сетки чата (`ChatPage.xaml`), без `x:Name`, без `ItemsUpdatingScrollMode`, без `ScrollTo`. `ChatPage.xaml.cs` на `Items` не подписан.
- **Avalonia.** `ChatView.axaml.cs`: `OnAppearing` подписывает `OnItemsChanged` на `Items.CollectionChanged`, тот откладывает `Transcript.ScrollToEnd()` до `DispatcherPriority.Background`. Прокручивает при любом изменении `Items`, не учитывает прокрутку пользователем, при обновлении карточки не прокручивает.
- **Как меняются строки** (`ChatPageModel`). `ReloadAsync` после каждого действия и при открытии очищает `Items` и строит заново. Во время генерации `OnGenerationUpdated` меняет карточку на месте (`card.Update` в UI-потоке), `Items` не трогает — для этого нужен отдельный сигнал модели (`CardUpdated`).
- **Картинка.** Зона картинки фиксирована (264×176 в обеих оболочках), её появление высоту не меняет; растут строки статуса, ошибки, мета, действия.
- **Решения (приняты владельцем).** (1) Любое изменение `Items` (открытие, отправка, «Повторить», «Новый seed», правка промпта) прокручивает в конец, даже если пользователь прокрутил вверх. (2) Обновление карточки и смена размера ленты (клавиатура, 095) прокручивают, только если лента «следует за концом». (3) Прокрутка пользователем вверх выключает следование, возврат к концу включает. (4) Правило — класс `TranscriptFollower` в `Presentation`, одно для обеих оболочек; Avalonia переводится на него.
- **Почему не `ItemsUpdatingScrollMode="KeepLastItemInView"`:** не покрывает рост карточки и прокрутку пользователем.

## План
Выполнять после: 095 (ручная проверка прокрутки при открытой клавиатуре), 096 (правит `ChatPageModelTests.cs`; конфликт только текстовый). Пересечения только текстовые: `ChatPage.xaml` — 088, 090, 091, 098; `ChatPage.xaml.cs` — 088, 090, 098; `ChatView.axaml(.cs)` — 098. 098 выполняется после этой задачи.

Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, членов, ключей строк и тестов переноси дословно. Если шаг не получается так, как написано (API другое, компилятор не принимает разметку), не придумывай обход: опиши в «Комментариях», что пробовал, поставь `blocked` и остановись.

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
   До шага 4 падает только `AtEnd_WindowShrunk_StaysAtEnd` (сейчас смена размера ленты не прокручивает); остальные три проходят и остаются регрессионными — это ожидаемо, не ошибка.
4. Avalonia `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml.cs`:
   - поля `private readonly TranscriptFollower _follower = new();` и `private ChatPageModel? _subscribed;`;
   - метод `private void ScrollToEndLater()` — тело нынешнего `OnItemsChanged` (отложенный `ScrollToEnd` с `DispatcherPriority.Background`);
   - `OnItemsChanged` → `if (_follower.RowsChanged()) ScrollToEndLater();`;
   - `private void OnCardUpdated(object? sender, EventArgs e)` → `if (_follower.ContentChanged()) ScrollToEndLater();`;
   - `private void OnTranscriptSizeChanged(object? sender, SizeChangedEventArgs e)` → `if (_follower.ViewportChanged()) ScrollToEndLater();`;
   - `private void OnTranscriptScrollChanged(object? sender, ScrollChangedEventArgs e)` → если `e.OffsetDelta.Y == 0` — выйти (изменился только размер содержимого, это не жест пользователя); иначе `_follower.UserScrolled(TranscriptFollower.IsAtEnd(t.Offset.Y, t.Viewport.Height, t.Extent.Height))`, где `t` — `ScrollViewer "Transcript"`;
   - `OnAppearing`: рядом с подпиской на `Items` — отписать и подписать (`-=` затем `+=`) `model.CardUpdated`, `Transcript.ScrollChanged`, `Transcript.SizeChanged`; запомнить `_subscribed = model`;
   - `OnDisappearing`: отписать всё то же (`CardUpdated` — у `_subscribed`), `_subscribed = null`.
   Тесты шага 3 проходят; прогнать все `ImageGen.Avalonia.Tests`.
5. MAUI, падающий текстовый тест. Новый файл `ImageGen/ImageGen.Presentation.Tests/Design/MauiChatPageMarkupTests.cs`, класс `MauiChatPageMarkupTests` (свой `MauiRoot()`, как в `MauiStylesTests`; XML через `XDocument`, имя `x:Name` — атрибут в пространстве `http://schemas.microsoft.com/winfx/2009/xaml`). Тест `Transcript_IsNamedCollectionView`: в `Pages/ChatPage.xaml` есть элемент `CollectionView` с `x:Name="Transcript"` и `ItemsSource="{Binding Items}"`.
6. MAUI `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`: ленте (`CollectionView Grid.Row="1"` с `ItemsSource="{Binding Items}"`) добавить `x:Name="Transcript"`. Остальное не менять. Тест шага 5 проходит.
7. MAUI `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml.cs`:
   - поля `private readonly TranscriptFollower _follower = new();`, `private bool _scrollPending;`;
   - `private void ScrollToEndLater()`: если `_scrollPending` — выйти; `_scrollPending = true`; `Dispatcher.Dispatch(...)`, внутри: `_scrollPending = false`; если `_model.Items.Count > 0` → `Transcript.ScrollTo(_model.Items.Count - 1, position: ScrollToPosition.End, animate: false)`;
   - `private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)` → `if (_follower.RowsChanged()) ScrollToEndLater();`;
   - `private void OnCardUpdated(object? sender, EventArgs e)` → `if (_follower.ContentChanged()) ScrollToEndLater();`;
   - `private void OnTranscriptSizeChanged(object? sender, EventArgs e)` → `if (_follower.ViewportChanged()) ScrollToEndLater();`;
   - `private void OnTranscriptScrolled(object? sender, ItemsViewScrolledEventArgs e)` → если `e.VerticalDelta == 0` — выйти; иначе `_follower.UserScrolled(e.LastVisibleItemIndex >= _model.Items.Count - 1)`;
   - `OnAppearing`: до вызова `_model.OnAppearingAsync()` — отписать и подписать (`-=` затем `+=`) `_model.Items.CollectionChanged`, `_model.CardUpdated`, `Transcript.Scrolled`, `Transcript.SizeChanged`;
   - `OnDisappearing`: отписать все четыре.
   Сборка Android и формат (см. «Проверку»).
8. Документация:
   - `docs/specs/chat-sessions.md`, раздел «### S2. Чат и ассистент», новый пункт `S2.7` в конце раздела (после S2.6 и его продолжения): «Лента прокручивается к последней строке при открытии сессии и при каждой новой строке; пока пользователь не прокрутил вверх, лента следует и за обновлениями карточек и сменой своего размера (клавиатура) (задача 097).»
   - `docs/knowledge/maui.md`, раздел «Разметка и списки»: пункт «**Прокрутка ленты чата (задача 097):**» — `ScrollTo(индекс, ScrollToPosition.End, animate: false)` через `Dispatcher.Dispatch` после изменения `Items`; жест пользователя — `Scrolled` с `VerticalDelta ≠ 0`, «в конце» — `LastVisibleItemIndex`; на устройстве проверено или нет.
   - `docs/architecture.md` не меняется (`Utilities/` уже описан).

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
Сборку и формат MAUI без Android SDK пропустить и записать в «Отчёт» (Windows-цель проверит CI).

### Ручная проверка
Android (если исполнитель не проверил — в «Отчёт» как «не проверено, проверить владельцу»):
1. Открыть длинную сессию → видна последняя строка.
2. Отправить промпт → видны своё сообщение, ответ ассистента и строка прогона; карточка не под полем ввода.
3. Во время генерации меняется статус карточки → нижний край последней карточки виден.
4. Прокрутить вверх во время генерации → лента не дёргается; вернуться вниз → снова следует.
5. В конце ленты открыть клавиатуру → последняя строка видна.
MAUI под Windows — те же пункты 1–4 дописать в `tasks/v0.1/016-skeleton-manual-check.md`. Avalonia — пункты 1–4 дописать в `tasks/backlog/046-desktop-manual-check.md`.

Файлы: `ImageGen/ImageGen.Presentation/Utilities/TranscriptFollower.cs` (новый), `ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs`, `ImageGen/ImageGen.Presentation.Tests/Utilities/TranscriptFollowerTests.cs` (новый), `ImageGen/ImageGen.Presentation.Tests/PageModels/ChatPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/Design/MauiChatPageMarkupTests.cs` (новый), `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml.cs`, `ImageGen/ImageGen.Avalonia.Tests/Views/TranscriptScrollTests.cs` (новый), `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml.cs`, `docs/specs/chat-sessions.md`, `docs/knowledge/maui.md`, `tasks/v0.1/016-skeleton-manual-check.md`, `tasks/backlog/046-desktop-manual-check.md`.

## Отчёт

## Комментарии
### 2026-10-29, claude-plan
- Вопрос владельцу: прокручивать ли ленту в конец после «Повторить», «Новый seed» или правки промпта у старой карточки, если пользователь прокрутил вверх? Допущение в плане: да, как сейчас в Avalonia: новый прогон появляется в конце, и его нужно видеть.
- Допущение: то же правило следования (`TranscriptFollower`) ставится и в Avalonia, иначе поведение оболочек разойдётся (сейчас Avalonia не следует за ростом карточки и не учитывает прокрутку вверх).

### 2026-10-29, владелец (в чате, записал claude-plan)
План одобрен; допущения из комментариев claude-plan приняты как решения. План переписан под небольшую модель.
