---
id: 076
status: done
assignee:
claimed:
created: 2026-10-28
---

# Карточка генерации: Details, Other model…, Copy prompt

## Описание
Решения владельца по 074: новые действия карточки (в строке значков, меню «…»/правого клика и нижнем меню телефона из 069):
- **Details** у ошибки — окно с исходным текстом ошибки (`Generation.ErrorMessage`); на карточке — короткая причина, как сейчас.
- **Other model…** у отфильтрованной (и у ошибки) — экран выбора модели: сверху быстрый выбор — модели картинок, которые уже использовались в этой сессии (кроме модели этой карточки); ниже раскрывающийся блок «All models» со всем каталогом и поиском. Выбор запускает прогон из одной генерации: тот же промпт, тот же сид, выбранная модель.
- **Copy prompt** — точный промпт генерации в буфер обмена (`IClipboardService` из 055).
Сохранение картинки — отдельная задача 080.

Справочник: `docs/design/implementation.md`; макеты: `docs/design/prototype.html` (https://claude.ai/artifact/M9QzPf39KLkRFTnYBtCi6F); решения владельца — задача 074 и «Комментарии» этой задачи.

Зависит от: 055 (`IClipboardService`), 069 (меню действий), 071 (те же файлы чата).
Среда: Linux или Windows (Avalonia — headless-тесты на Linux; MAUI — сборка Android на Linux с Android SDK, Windows-цель собирает CI; ручные проверки — 016 и 046).

## Критерии готовности
- Details виден только у карточек со статусом «ошибка»; Other model… — у «ошибки» и «отфильтровано»; Copy prompt — у всех завершённых.
- Экран выбора модели: на широком окне — диалог/панель, на узком — отдельный экран; «Used in this session» сверху, «All models» свёрнут по умолчанию, поиск фильтрует только «All models».
- Новый прогон помечен происхождением `RunOrigin.OtherModel` и показывается в чате как остальные (S4.4).
- `docs/specs/chat-sessions.md` S4 дополнен (S4.5 Other model, S4.6 Copy prompt, S4.7 Details).
- Тесты из «Плана» проходят.

## Анализ
- Ядро: `GenerationService` (`RepeatAsync`, `NewSeedAsync`, `EditAsync` — образец: `GetSourceAsync`, `GetRunParametersAsync`, `CreateRunAsync`). Новый `RunOnModelAsync(Guid generationId, ModelRef model, CancellationToken)` и значение `RunOrigin.OtherModel` (проверить, хранится ли `RunOrigin` числом в БД: новое значение добавлять в конец перечисления).
- Если модель не поддерживает сид (каталог), сид не отправляется — та же логика, что S3.3 (её делает `CreateRunAsync`/планировщик параметров — проверить).
- Модели, использованные в сессии: `IRunRepository.ListGenerationsBySessionAsync` → различные `ModelRef` (порядок — по последнему использованию). Каталог: `ModelCatalogService.GetAllImageModelsAsync(forceRefresh: false, …)`.
- Задача 043 (тексты ошибок в Presentation) не в очереди; Details показывает `ErrorMessage` как есть. Если 043 выполнена раньше — показывать исходный текст из того поля, которое она оставит для исходного сообщения провайдера.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, ключей и строк переноси дословно. Если шаг не получается так, как написано (API другое, компилятор не принимает разметку), не придумывай обход: опиши в «Комментариях», что пробовал, поставь `blocked` и остановись.

1. Core: `RunOrigin.OtherModel`; `GenerationService.RunOnModelAsync`. Тесты `GenerationServiceTests`: `RunOnModelAsync_UsesSamePromptAndSeed_NewModel`, `RunOnModelAsync_ModelWithoutSeed_DoesNotSendSeed`, `RunOnModelAsync_SetsOtherModelOrigin`.
2. Presentation:
   - `Routes.ModelPicker = "modelpicker"`, параметр `generationId` (`Routes.ModelPickerFor(Guid sessionId, Guid generationId)`).
   - `PageModels/ModelPickerPageModel.cs`: `UsedModels` и `AllModels` (`ObservableCollection<ModelOptionViewModel>`; `ModelOptionViewModel` уже есть — переиспользовать, если подходит), `IsAllExpanded`, `SearchText` (фильтр `AllModels` по названию без учёта регистра), `PickCommand(ModelOptionViewModel)` → `RunOnModelAsync`, затем `GoBackAsync`; `LoadCommand`.
   - `GenerationItemViewModel`: `DetailsCommand` (только при ошибке: `AlertAsync(AppResources.ErrorDetailsTitle = "Error details", ErrorText)`), `OtherModelCommand` (при ошибке или фильтре: навигация на `ModelPickerFor`), `CopyPromptCommand` (`IClipboardService.SetTextAsync(Prompt)`); подписи в меню действий 069.
   - Строки: `ErrorDetailsTitle`, `OtherModel` = "Other model…", `CopyPrompt` = "Copy prompt", `UsedInSession` = "Used in this session", `AllModels` = "All models", `ModelPickerTitle` = "Run on another model".
   - Тесты: `ModelPickerPageModelTests` — `Load_UsedModels_ExcludeSourceModel`, `Load_UsedModels_OrderedByLastUse`, `Search_FiltersAllModelsOnly`, `Pick_StartsRunAndGoesBack`; `GenerationItemViewModelTests` — `Details_OnlyForFailed`, `OtherModel_ForFailedAndFiltered`, `CopyPrompt_CopiesExactPrompt`.
3. Avalonia: `Views/ModelPickerView.axaml` (раздел `TextBlock.section` «Used in this session», список кнопок-строк; `Expander` «All models» с поиском и списком), маршрут в `AppServices`; пункты в меню карточки и строке значков (значки `Copy`, `Info` для Details).
4. MAUI: `Pages/ModelPickerPage.xaml`, `AddTransientWithShellRoute`, те же пункты в меню карточки.
5. Тест Avalonia `Flows/OtherModelFlowTests.cs`: `FilteredCard_OtherModel_StartsRunOnPickedModel` (через `OpenRouterFakes`).
6. `docs/specs/chat-sessions.md` S4.5–S4.7.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android -p:AndroidSdkDirectory=$ANDROID_HOME
dotnet format ImageGen/<каждый изменённый проект>/<проект>.csproj --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/{Domain/Generations/RunOrigin.cs,Services/GenerationService.cs}`, `ImageGen/ImageGen.Presentation/{Routes.cs,PageModels/ModelPickerPageModel.cs,PageModels/GenerationItemViewModel.cs,PageModels/ChatPageModel.cs}`, строки, `ImageGen/ImageGen.Avalonia/{Views/ModelPickerView.*,Views/ChatView.axaml,Composition/AppServices.cs}`, `ImageGen/ImageGen.Maui/{Pages/ModelPickerPage.*,Pages/ChatPage.xaml,MauiProgram.cs}`, тесты, `docs/specs/chat-sessions.md`

## Отчёт

Сделано по «Плану», по TDD.

Core: добавлено `RunOrigin.OtherModel = 4` (в конец перечисления — `RunOrigin`
хранится в БД как `(int)`, см. `RunRepository`). `GenerationService.RunOnModelAsync(
Guid generationId, ModelRef model, CancellationToken)` — прогон из одной генерации:
тот же промпт и сид источника (и те же референсы), выбранная модель, origin
`OtherModel`; `SeedApplied` ставит `CreateRunAsync` по `image?.SupportsSeed`, поэтому
для модели без сида сид не отправляется (S3.3). Тесты `GenerationServiceTests`:
`RunOnModelAsync_UsesSamePromptAndSeed_NewModel`, `RunOnModelAsync_ModelWithoutSeed_
DoesNotSendSeed`, `RunOnModelAsync_SetsOtherModelOrigin`.

Presentation: `Routes.ModelPicker = "modelpicker"` + параметр `generationId` и
`Routes.ModelPickerFor(sessionId, generationId)`. Новый `ModelPickerPageModel`:
`UsedModels`/`AllModels` (`ObservableCollection<ModelOptionViewModel>` —
переиспользован как есть), `IsAllExpanded`, `SearchText` (фильтр только `AllModels`
по имени/id без учёта регистра), `LoadCommand`, `PickCommand(ModelOptionViewModel)`
→ `RunOnModelAsync` + `GoBackAsync`, `ToggleAllCommand` (для оболочек без экспандера).
`UsedModels` — различные модели картинок сессии через
`IRunRepository.ListGenerationsBySessionAsync`, кроме модели карточки, по последнему
использованию; каталог — `ModelCatalogService.GetAllImageModelsAsync(forceRefresh:
false)`; модель из сессии, которой нет в каталоге, в быстрый список не попадает.
`GenerationItemViewModel`: `DetailsCommand` (только ошибка, `AlertAsync(
ErrorDetailsTitle, ErrorText)`), `OtherModelCommand` (ошибка/фильтр, навигация на
`ModelPickerFor`), `CopyPromptCommand` (все завершённые, `IClipboardService.SetTextAsync(
Prompt)`); новые пункты добавлены и в меню действий 069 (`ShowActionsAsync`).
Строки: `ErrorDetailsTitle`, `OtherModel`, `CopyPrompt`, `UsedInSession`, `AllModels`,
`ModelPickerTitle`. Тесты: `ModelPickerPageModelTests` (`Load_UsedModels_
ExcludeSourceModel`, `Load_UsedModels_OrderedByLastUse`, `Search_FiltersAllModelsOnly`,
`Pick_StartsRunAndGoesBack`), `GenerationItemViewModelTests` (`Details_OnlyForFailed`,
`Details_ShowsErrorText`, `OtherModel_ForFailedAndFiltered`, `CopyPrompt_
CopiesExactPrompt`).

Avalonia: `Views/ModelPickerView.axaml` (секция «Used in this session» со строками-
кнопками, `Expander` «All models» с поиском и списком); маршрут в `AppServices`
(+ `ModelPickerPageModel` транзиентом и в `Page`-свитче). В карточке чата: в меню
(контекст и «…») добавлены Copy prompt, Other model… (видимо при ошибке/фильтре),
Details (видимо при ошибке); в строке значков — Copy (`&#xE09E;`) и Details с
`Info` (`&#xE0F9;`, видимо при ошибке). Flow-тест `Flows/OtherModelFlowTests.cs`:
`FilteredCard_OtherModel_StartsRunOnPickedModel` (через `OpenRouterFakes` с опцией
`imagesFiltered`). Обновлены тесты `CardActionsTests` под новые пункты/значки.

MAUI: `Pages/ModelPickerPage.xaml(.cs)` (секция + сворачиваемый «All models» по
`IsAllExpanded`/`ToggleAllCommand`, строки-кнопки с `Command` через `x:Reference
Page`), регистрация `AddTransientWithShellRoute<ModelPickerPage, ModelPickerPageModel>`.
В `ChatPage.xaml` MenuFlyout дополнен Copy prompt / Other model… / Details
(видимость через `IsEnabled`; быстрый выбор экшн-листа — через `ShowActionsCommand`).

Автономные решения:
- Для MAUI «All models» сделан сворачиваемым через `Button` + `IsVisible`
  (а не `toolkit:Expander`), чтобы не завязываться на компонент и не ловить XAML-
  грабли; в Avalonia использован штатный `Expander`. Поведение совпадает с решением
  владельца (быстрый выбор + экспандер со всем каталогом).
- Other model… значка в строке иконок не получил (в `IconGlyphs` подходящего нет):
  доступен из меню «…»/контекста и экшн-листа; в строке иконок добавлены только
  Copy и Details (`Info`), как указано в плане.

Проверка локально (.NET 10 SDK 10.0.403; MAUI android workload + Android SDK; JDK 21):
`dotnet build ImageGen.Maui -f net10.0-android` — успешно, 0 warnings/0 errors.
Core.Tests 310/310, Data.Tests 63/63, Presentation.Tests 223/223,
Avalonia.Tests 103/103 (2 пропуска окружения). `dotnet format --verify-no-changes`
по каждому изменённому проекту (Core, Core.Tests, Presentation, Presentation.Tests,
Avalonia, Avalonia.Tests, Maui) — без изменений. `scripts/check.sh` — пройден.
Не проверено вручную: Android/Windows-рантайм (только сборка), macOS/iOS.

## Комментарии

### 2026-10-28, владелец (в чате, записал claude-cloud-t9wd)
Other model…: «модели, которые хотя бы раз использовались в сессии — в быстрый выбор, все остальные — под экспандер».
