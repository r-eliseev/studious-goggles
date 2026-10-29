---
id: 097
status: planned
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
- **MAUI.** Лента — `CollectionView` в строке 1 сетки чата (`ChatPage.xaml`), без `x:Name`, без `ItemsUpdatingScrollMode` и без вызовов `ScrollTo`. Код страницы (`ChatPage.xaml.cs`) на `Items` не подписан.
- **Avalonia.** `ChatView.axaml.cs`: в `OnAppearing` подписка на `Items.CollectionChanged`, `OnItemsChanged` откладывает `ScrollViewer Transcript.ScrollToEnd()` до `DispatcherPriority.Background` (ждёт раскладку новой строки). Прокрутка идёт при любом изменении `Items`, без учёта того, где пользователь. При изменении карточки прокрутки нет.
- **Как меняются строки** (`ChatPageModel`). `ReloadAsync` после каждого действия (открытие, отправка, повтор, новый seed, правка промпта, повтор ошибки) очищает `Items` и строит их заново, затем может вставить `SummaryMarkerViewModel`. Значит, «новые строки» — это `Reset` и серия `Add`. Во время генерации `OnGenerationUpdated` меняет карточку на месте (`card.Update` в UI-потоке), `Items` не трогает. Поэтому только на `Items` прокрутку при обновлении карточки не поймать: нужен отдельный сигнал от модели.
- **Растёт ли карточка, когда приходит картинка.** Зона картинки фиксирована: 264×176 в обеих оболочках (`Border` с `WidthRequest`/`HeightRequest` в MAUI, `Width`/`Height` в Avalonia). Картинка высоту не меняет. Меняются строки статуса, текст ошибки, мета и кнопки действий, а после 096 и сводка прогона. Рост небольшой, но нижний край последней карточки может уйти под поле ввода.
- **Решение по открытым вопросам.** (1) Изменение `Items` (то есть результат действия пользователя или открытие сессии) всегда прокручивает в конец, как сейчас в Avalonia. (2) Обновление карточки прокручивает, только если лента «следует за концом»: пользователь не прокручивал вверх. (3) Прокрутка пользователем выключает следование, возврат к концу включает его снова. (4) Если лента следует, изменение размера ленты (открылась клавиатура, задача 095) тоже прокручивает в конец. Это общее правило, поэтому оно выносится в `Presentation` и тестируется там, а оболочки только передают события и вызывают прокрутку. Avalonia получает то же поведение (иначе критерий «совпадает с Avalonia» выполняется только для строк, но не для роста карточки).
- **Почему не `ItemsUpdatingScrollMode="KeepLastItemInView"`.** Он срабатывает только на изменения коллекции: рост карточки не покрывает, пользователя, прокрутившего вверх, не учитывает. Как он ведёт себя на `Reset` под Android, неизвестно. Выбран явный `ScrollTo(последний индекс, ScrollToPosition.End, animate: false)`.
- **Неизвестно (проверка на устройстве):** успевает ли `ScrollTo`, вызванный через `Dispatcher.Dispatch` после `Reset`/`Add`, попасть в уже размеченный список на Android; если нет, повторить прокрутку после раскладки (событие `SizeChanged`/`LayoutChanged` ленты) или с небольшой задержкой. Насколько надёжен `ItemsViewScrolledEventArgs.LastVisibleItemIndex` на Android и Windows.
- **Пересечения.** 095 и 098 правят `ChatPage.xaml` (корень и шапка; эта задача — лента). 088 — блок действий карточки в `ChatPage.xaml`. 096 правит `ChatPageModelTests.cs` и карточки; здесь в `ChatPageModel` добавляется событие, в тесты — новые случаи. При rebase сохранить обе правки. Порядок: **095 → 097 → 098**.

## План
TDD: каждый шаг — сначала падающий тест, затем минимальный код.

1. `Presentation/Utilities/TranscriptFollower` — правило «следовать за концом ленты» без UI-типов. Состояние: `IsFollowing` (в начале `true`). Члены:
   - `RowsChanged()` → `IsFollowing ← true`, вернуть «прокрутить»;
   - `ContentChanged()` (карточка обновилась) → вернуть `IsFollowing`;
   - `ViewportChanged()` (лента сменила размер) → вернуть `IsFollowing`;
   - `UserScrolled(atEnd)` → `IsFollowing ← atEnd`.
   Тесты (`ImageGen.Presentation.Tests/Utilities/TranscriptFollowerTests`): новый → `ContentChanged` даёт «прокрутить»; после `UserScrolled(false)` → `ContentChanged` и `ViewportChanged` не прокручивают; после `UserScrolled(false)` → `RowsChanged` прокручивает и включает следование; `UserScrolled(false)`, затем `UserScrolled(true)` → снова следует.
2. `ChatPageModel`: событие `CardUpdated`, поднимается в `OnGenerationUpdated` после `card.Update` в UI-потоке, только для карточки этой сессии, которая есть в `_cards`. Тесты в `ChatPageModelTests` (двойники как в `GeneratingCard_UpdatesInPlaceWithoutReload`): обновление генерации этой сессии → событие поднято; генерация другой сессии → не поднято; после `OnDisappearing` → не поднято.
3. Avalonia `ChatView.axaml.cs`: один `TranscriptFollower` на представление. `OnItemsChanged` → `RowsChanged`, затем отложенный `ScrollToEnd` (как сейчас). `CardUpdated` и `SizeChanged` у `Transcript` → `ContentChanged`/`ViewportChanged`, прокрутка только при «да». `Transcript.ScrollChanged`: учитывать только изменение смещения (`OffsetDelta.Y ≠ 0`). Если изменился только размер содержимого (`ExtentDelta`), это рост, а не жест пользователя. `atEnd` = смещение + высота окна ≥ высота содержимого − небольшой допуск. Подписки — в `OnAppearing`, отписки — в `OnDisappearing`, как у `Items`. Headless-тесты (`ImageGen.Avalonia.Tests/Views`, на `ShellHarness`, как `ChatLayoutTests`): сессия с длинной историей при открытии → `Transcript` в конце; пользователь прокрутил вверх, пришло обновление карточки → смещение не изменилось; прокрутил вверх, затем отправил сообщение (`Items` перестроены) → в конце.
4. MAUI `ChatPage`: у ленты `x:Name="Transcript"`; в `ChatPage.xaml.cs` тот же `TranscriptFollower`. Подписки в `OnAppearing` и отписки в `OnDisappearing`: `Items.CollectionChanged`, `CardUpdated`, `Transcript.Scrolled`, `Transcript.SizeChanged`. Прокрутка: если `Items` не пуст → `Dispatcher.Dispatch(() => Transcript.ScrollTo(Items.Count − 1, position: End, animate: false))`. При серии `Add` после `Reset` достаточно одной прокрутки на цикл диспетчера (флаг «прокрутка уже запланирована»). `Scrolled`: при `VerticalDelta ≠ 0` → `UserScrolled(LastVisibleItemIndex ≥ Items.Count − 1)`. Логики сверх вызовов `TranscriptFollower` в коде страницы нет.
5. Ручная проверка на Android (после 095): открыть длинную сессию → видна последняя строка; отправить промпт → видны своё сообщение, ответ ассистента и строка прогона, карточка не под полем ввода; во время генерации нижний край последней карточки виден, когда меняется статус; прокрутить вверх во время генерации → лента не дёргается; вернуться вниз → снова следует; открыть клавиатуру в конце ленты → последняя строка остаётся видна. Если устройства нет — пункт в `tasks/v0.1/016-skeleton-manual-check.md` (MAUI) и `tasks/backlog/046-desktop-manual-check.md` (Avalonia).
6. Документация: `docs/specs/chat-sessions.md` — одна строка о поведении ленты (в конец при новых строках, следование за обновлениями карточек, пока пользователь не прокрутил вверх). `docs/knowledge/maui.md` — что выяснилось про `ScrollTo` и `Scrolled` на Android. `docs/architecture.md` не меняется (`Utilities/` в ней есть).

Проверка:
- `dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj`
- `dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj`
- `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android` (если есть Android SDK); Windows-цель — CI
- `dotnet format --verify-no-changes` для `ImageGen.Presentation`, `ImageGen.Presentation.Tests`, `ImageGen.Avalonia`, `ImageGen.Avalonia.Tests`, `ImageGen.Maui` (последний — при Android SDK)
- `scripts/check.sh`

Файлы: `ImageGen/ImageGen.Presentation/Utilities/TranscriptFollower.cs`, `ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs`, `ImageGen/ImageGen.Presentation.Tests/Utilities/TranscriptFollowerTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ChatPageModelTests.cs`, `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml.cs`, `ImageGen/ImageGen.Avalonia.Tests/Views/` (новый файл тестов прокрутки), `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml.cs`; `docs/specs/chat-sessions.md`, `docs/knowledge/maui.md`; при необходимости `tasks/v0.1/016-skeleton-manual-check.md`.

## Отчёт

## Комментарии
### 2026-10-29, claude-plan
- Вопрос владельцу: прокручивать ли ленту в конец после «Повторить», «Новый seed» или правки промпта у старой карточки, если пользователь прокрутил вверх? Допущение в плане: да, как сейчас в Avalonia: новый прогон появляется в конце, и его нужно видеть.
- Допущение: то же правило следования (`TranscriptFollower`) ставится и в Avalonia, иначе поведение оболочек разойдётся (сейчас Avalonia не следует за ростом карточки и не учитывает прокрутку вверх).
