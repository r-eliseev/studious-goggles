---
id: 096
status: in-progress
assignee: codex-luna
claimed: 2026-10-30T08:56:36Z
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
- Пересечения: 138 (общая проверка `GenerationItemViewModel.ShowActionsCommand`) правит тот же файл `GenerationItemViewModel.cs`, но другие члены; 097 и 098 трогают `ChatPage.xaml`/`ChatView.axaml`, эта задача их не меняет. Порядок не важен; при конфликте в `GenerationItemViewModel.cs` при rebase сохранить обе правки. 114 и 123 добавляют тесты в `ChatPageModelTests.cs`; это текстовое пересечение без функциональной зависимости: сохранить обе группы тестов, использовать обновлённые helpers.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, членов, ключей строк и тестов переноси дословно. При расхождении с текущим API адаптируй имена и разметку в рамках контракта, исправь ошибку минимально и проверь сборкой. `blocked` нужен для неизвестного продуктового решения или недоступной обязательной среды, а не для рутинной ошибки компиляции.

Выполнять после: — (138 правит тот же `GenerationItemViewModel.cs`, но другие члены; порядок любой, при конфликте при rebase сохранить обе правки).

Прочитай перед началом: `docs/conventions.md` (MVVM, `CommunityToolkit.Mvvm`); `ImageGen/ImageGen.Presentation/PageModels/RunItemViewModel.cs`; `ImageGen/ImageGen.Presentation/PageModels/GenerationItemViewModel.cs` (свойство `CostUsd`, метод `Update(Generation)`); `ImageGen/ImageGen.Presentation/PageModels/MessageItemViewModel.cs` (образец `sealed partial class … : ObservableObject`); тесты `ImageGen/ImageGen.Presentation.Tests/PageModels/RunItemViewModelTests.cs` и тест `GeneratingCard_UpdatesInPlaceWithoutReload` в `ChatPageModelTests.cs`.

1. Тест `GenerationItemViewModelTests.Update_CostArrives_RaisesCostUsdChanged` (файл `ImageGen/ImageGen.Presentation.Tests/PageModels/GenerationItemViewModelTests.cs`, использовать существующие `BaseGeneration()` и `CardOf(Generation, …)`): генерация со `Status = Running`, `CostUsd = null`, карточка из неё; подписаться на `PropertyChanged` и собирать имена свойств; поменять у той же генерации `Status = Succeeded`, `CostUsd = 0.014m` и вызвать `card.Update(generation)` → среди имён есть `nameof(GenerationItemViewModel.CostUsd)`, `card.CostUsd == 0.014m`. Запустить — тест падает.
2. В `GenerationItemViewModel` заменить `public decimal? CostUsd { get; private set; }` на наблюдаемое свойство `[ObservableProperty] public partial decimal? CostUsd { get; private set; }` (XML-комментарий оставить). Присваивание в `Update` не менять. Если генератор не принимает `private set` (ошибка компиляции из `CommunityToolkit.Mvvm`) → используй backing field `_costUsd` и `SetProperty` в private setter, сохранив внешний read-only контракт; публичный setter не открывать. Тест шага 1 проходит.
3. Тесты в `RunItemViewModelTests` (падают, пока `RunItemViewModel` не сообщает об изменениях). Сначала вспомогательные методы в том же классе: `private static Generation Gen(string modelId, GenerationStatus status, decimal? cost)` (поля как в существующем `Card`) и `private static GenerationItemViewModel CardOf(Generation generation)`; существующий `Card(string, GenerationStatus, decimal?)` переписать через них (поведение то же). Ожидаемый текст — всегда `string.Format(AppResources.RunHeaderFormat, models, runsPerModel, done, total, price)`, как в `HeaderText_CountsDoneAndCost`. Тесты:
   - `HeaderText_CardBecomesDone_UpdatesCountAndCost`: две генерации `vendor/a` в Queued без цены, строка из двух карточек, подписка на `PropertyChanged` строки; первой генерации поставить Succeeded и 0.014m, `Update` первой карточки → `HeaderText` = формат с (1, 2, 1, 2, "0.014"), среди имён событий есть `nameof(RunItemViewModel.HeaderText)`.
   - `HeaderText_PriceChangesAfterDone_UpdatesCost`: карточка уже Done, пришла уточнённая цена при том же статусе → новая сумма и `PropertyChanged(HeaderText)`; обновление только цены должно работать без изменения `StatusKind`.
   - `HeaderText_AllCardsDone_SumsCost`: то же, обе карточки → Succeeded по 0.014m → (1, 2, 2, 2, "0.028").
   - `HeaderText_CardFails_DoneCountUnchanged`: две карточки в Running, одна → Failed без цены → (1, 2, 0, 2, "0").
   - `HeaderText_SummaryUnchanged_RaisesNothing`: карточка в Queued, `Update` той же генерацией без изменений → события `HeaderText` нет.
   - `HeaderText_RestoredRun_EqualsLiveRun`: строка A из двух карточек, созданных сразу в Succeeded с 0.014m; строка B из двух карточек в Queued, затем обе переведены в Succeeded с 0.014m через `Update` → `A.HeaderText == B.HeaderText`.
4. `RunItemViewModel` (`ImageGen/ImageGen.Presentation/PageModels/RunItemViewModel.cs`): объявление класса → `public sealed partial class RunItemViewModel : ObservableObject` (using `CommunityToolkit.Mvvm.ComponentModel`). `HeaderText` → `[ObservableProperty] public partial string HeaderText { get; private set; }` (XML-комментарий оставить; при несовместимости генератора — private backing field и `SetProperty` в private setter, внешний read-only контракт сохранить). В конструкторе после `HeaderText = HeaderOf(generations)` подписаться на `PropertyChanged` каждой карточки обработчиком `private void OnCardPropertyChanged(object? sender, PropertyChangedEventArgs e)`. Псевдокод обработчика: если `e.PropertyName` пустое или null, или равно `nameof(GenerationItemViewModel.StatusKind)`, `nameof(GenerationItemViewModel.CostUsd)`, `nameof(GenerationItemViewModel.ModelText)` → `HeaderText = HeaderOf(Generations)`; иначе ничего. Отписку не добавлять: строка и её карточки живут и выбрасываются вместе (`ChatPageModel.ReloadAsync`). `HeaderOf` не менять. Тесты шага 3 проходят.
5. Тест `ChatPageModelTests.GeneratingCard_Done_UpdatesRunHeader` (рядом с `GeneratingCard_UpdatesInPlaceWithoutReload`, ту же подготовку скопировать): после `release.SetResult(new ImageGenerationResult(Image, "image/png", 0.014m))` и `WhenIdleAsync()` у той же строки (`_model.Items.Last()` как `RunItemViewModel`, `Assert.Same` с запомненной до результата) `HeaderText` содержит «2 of 2 done» и заканчивается на «$0.028». Должен пройти сразу после шага 4; если падает — искать причину в шагах 2–4, не в `ChatPageModel`.
6. Разметку не менять: `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml` (`Label Text="{Binding HeaderText}"`) и `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml` (`TextBlock Text="{Binding HeaderText}"`) уже привязаны однонаправленно. Только убедиться, что в них нет `Mode=OneTime`; если есть → убрать `Mode=OneTime`.
7. Документация: `docs/specs/chat-sessions.md` — найти поиском «done» или «сводк» описание строки прогона; если оно есть → дописать в него «обновляется при каждом изменении статуса или цены карточки»; если нет → спецификацию не трогать. `docs/architecture.md` не меняется.
8. «Отчёт»: что сделано, тесты, и строка «Android, Windows, Avalonia-окно: живое обновление сводки не проверено вручную, проверить владельцу».

Завершение: заполни «Отчёт» с изменениями, командами и результатами проверок, передай ручные сценарии в 016 (Windows), 046 (Avalonia), 140 (Android) по применимости. Выполни `scripts/release-task.sh 096`, затем `bash scripts/check.sh`. Недоступная ручная проверка не заменяет сборку и не оставляет готовую задачу в `in-progress`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

### Ручная проверка
Отправить промпт на 2 модели × 2: строка над прогоном меняется с «0 of 4 done · $0» по мере готовности карточек и в конце показывает сумму цен карточек; после перезапуска приложения строка та же. Дописать пунктом в `tasks/v0.1/016-skeleton-manual-check.md` (MAUI под Windows) и `tasks/backlog/046-desktop-manual-check.md` (Avalonia); Android — в 140 с отметкой проверки в «Отчёте».

Файлы: `ImageGen/ImageGen.Presentation/PageModels/RunItemViewModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/GenerationItemViewModel.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/RunItemViewModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/GenerationItemViewModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ChatPageModelTests.cs`, `tasks/v0.1/016-skeleton-manual-check.md`, `tasks/backlog/046-desktop-manual-check.md`, `tasks/backlog/140-*.md`; при необходимости `docs/specs/chat-sessions.md`.

## Отчёт

## Комментарии

### 2026-10-29, владелец (в чате, записал claude-plan)
План одобрен; допущения из комментариев claude-plan приняты как решения. План переписан под небольшую модель.
