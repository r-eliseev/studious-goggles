---
id: 096
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Сводка прогона не обновляется по ходу генерации

## Описание
Замечание владельца (2026-10-29, Android, скриншоты): строка над прогоном «2 models × 2 · 0 of 4 done · $0» так и остаётся «0 of N done · $0», хотя карточки уже Done и с ценой.

Что известно: `RunItemViewModel.HeaderText` (`ImageGen.Presentation/PageModels/RunItemViewModel.cs`) считается один раз в конструкторе по карточкам на момент создания строки и не уведомляет об изменениях; карточки `GenerationItemViewModel` меняют статус и цену позже. Код общий, поэтому, скорее всего, то же в Avalonia (`ChatView.axaml` привязан к тому же `HeaderText`).

Зависит от: —
Среда: Linux или Windows.

## Критерии готовности
- Число готовых и сумма в сводке обновляются при каждом изменении статуса или цены любой карточки прогона, в обеих оболочках.
- Сводка после перезапуска (восстановление из истории) совпадает со сводкой в живой сессии.
- Тест на уровне `Presentation`: карточка переходит в Done с ценой → `HeaderText` меняется и поднимается `PropertyChanged`.

## Анализ
- Причина подтверждена по коду: `RunItemViewModel` — обычный класс без `INotifyPropertyChanged`; `HeaderText` — свойство только для чтения, считается в конструкторе (`HeaderOf`) по карточкам на момент создания строки.
- Как живут строки: `ChatPageModel.ReloadAsync` очищает `Items` и `_cards`, на каждый прогон создаёт карточки (`CreateCard` → конструктор `GenerationItemViewModel` вызывает `Update(generation)`) и строку `RunItemViewModel`. Дальше карточки меняются на месте: `OnGenerationUpdated` → `mainThread.BeginInvokeOnMainThread` → `card.Update(generation)`; строка прогона при этом не пересоздаётся (тест `GeneratingCard_UpdatesInPlaceWithoutReload` проверяет, что `Items.Last()` тот же объект). Поэтому сводка застывает на «0 of N done · $0».
- `GenerationItemViewModel.StatusKind` и `ModelText` уже наблюдаемые (`[ObservableProperty]`), а `CostUsd` — нет (`{ get; private set; }`, меняется в `Update` без уведомления). `CostText` уведомляет, но это строка для показа.
- Восстановление из истории: при перезапуске карточки создаются из сохранённых генераций и сразу получают итоговый статус и цену, поэтому сводка после перезапуска уже верная; живая сессия после исправления будет считать по той же формуле `HeaderOf` из тех же полей карточек, значит, совпадёт.
- Разметка: MAUI `ChatPage.xaml` (`Label Text="{Binding HeaderText}"`, `x:DataType="pm:RunItemViewModel"`) и Avalonia `ChatView.axaml` (`TextBlock Text="{Binding HeaderText}"`) привязаны однонаправленно; обе подхватят `PropertyChanged`, разметку менять не нужно.
- Потоки: `Update` вызывается на UI-потоке, значит, и пересчёт сводки с `PropertyChanged` — тоже на UI-потоке.
- Утечки подписок нет: строка подписывается на события своих карточек, и они живут вместе (обе выбрасываются при `ReloadAsync`).
- Пересечения: 088 (`GenerationItemViewModel.ShowActionsCommand`, `ChatPage.xaml`) правит тот же файл `GenerationItemViewModel.cs`, но другие члены; 097 и 098 трогают `ChatPage.xaml`/`ChatView.axaml`, эта задача их не меняет. Порядок не важен; при конфликте в `GenerationItemViewModel.cs` при rebase сохранить обе правки.

## План
TDD: каждый шаг — сначала падающий тест, затем минимальный код.

1. `GenerationItemViewModel.CostUsd` становится наблюдаемым: `[ObservableProperty]` с закрытым сеттером (или явный `OnPropertyChanged(nameof(CostUsd))` в `Update`), смысл и значение прежние.
   - Тест (`GenerationItemViewModelTests`): карточка в Running без цены, `Update` той же генерацией в Succeeded с ценой 0.014 → поднято `PropertyChanged` с именем `CostUsd`, `CostUsd == 0.014`.
2. `RunItemViewModel` становится `ObservableObject` (`sealed partial`). `HeaderText` — наблюдаемое свойство с закрытым сеттером. В конструкторе: посчитать `HeaderText = HeaderOf(generations)` и подписаться на `PropertyChanged` каждой карточки; обработчик: если имя свойства `StatusKind`, `CostUsd` или `ModelText` (или пустое/null — «изменилось всё») → `HeaderText = HeaderOf(Generations)`. Сеттер, созданный `[ObservableProperty]`, не поднимает событие, если строка не изменилась.
   - Псевдокод: `OnCardChanged(e): if e.PropertyName in {StatusKind, CostUsd, ModelText, null, ""} then HeaderText ← HeaderOf(Generations)`.
   - Тесты (`RunItemViewModelTests`, карточки через существующий `MakeCard`/`Card` и `card.Update(...)`):
     - две карточки в Queued, одна переходит в Succeeded с ценой 0.014 → `HeaderText` равен строке формата `RunHeaderFormat` с «1 of 2 done» и «$0.014», а `PropertyChanged` для `HeaderText` поднято хотя бы раз;
     - обе карточки Succeeded с ценой 0.014 → «2 of 2 done · $0.028»;
     - карточка меняет только `SeedText`/`TimeText` при том же статусе и цене → событие `HeaderText` не поднимается (строка та же);
     - карточка из Running переходит в Failed без цены → число готовых не растёт, сумма прежняя;
     - сводка строки, созданной из уже завершённых карточек (как при восстановлении), равна сводке строки, чьи карточки дошли до того же состояния через `Update` (критерий «после перезапуска совпадает»).
3. Интеграционный тест в `ChatPageModelTests` (рядом с `GeneratingCard_UpdatesInPlaceWithoutReload`, на тех же двойниках): прогон из двух карточек в Running, генератор возвращает результат с ценой 0.014 → после `WhenIdleAsync` у той же строки `RunItemViewModel` (`Items.Last()`) `HeaderText` содержит «2 of 2 done» и «$0.028»; строка не пересоздана.
4. Разметку не трогать; убедиться, что привязки в `ChatPage.xaml` и `ChatView.axaml` однонаправленные (они такие сейчас). Если Avalonia-тесты чата проверяют текст сводки, прогнать их; новый headless-тест не нужен — поведение покрыто на уровне `Presentation`.
5. Спецификации: в `docs/specs/chat-sessions.md` сводка прогона как поведение не описана (только формат строки в ресурсах); если при реализации окажется описана — уточнить, что она обновляется по ходу. `docs/architecture.md` не меняется.
6. Ручная проверка (MAUI Android и Windows, Avalonia): сводка меняется по мере готовности карточек — пункт дописать в 016 (MAUI) и 046 (Avalonia), если нельзя проверить в своей среде.

Проверка:
- `dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj`
- `dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj`
- `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android` (если есть Android SDK)
- `dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes` и то же для `ImageGen.Presentation.Tests`
- `scripts/check.sh`

Файлы: `ImageGen/ImageGen.Presentation/PageModels/RunItemViewModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/GenerationItemViewModel.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/RunItemViewModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/GenerationItemViewModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ChatPageModelTests.cs`; при необходимости ручные пункты в `tasks/v0.1/016-skeleton-manual-check.md` и задаче 046.

## Отчёт

## Комментарии
