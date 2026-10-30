---
id: 092
status: in-progress
assignee: codex-gpt6
claimed: 2026-10-30T11:10:52Z
created: 2026-10-29
---

# Модели сессии: группы по производителю, раскрывающиеся

## Описание
Замечание владельца (2026-10-29): модели в списке нужно сгруппировать по производителю (Black Forest Labs, Google, OpenAI, xAI и т. д.) и показывать группы раскрывающимися списками. Касается обеих вкладок из 090: и моделей картинок, и моделей ассистента.

Откуда надёжно брать производителя, неизвестно: префикс id до `/` (`black-forest-labs/flux.2-klein-4b`, `x-ai/grok-imagine-image-2.0`) или часть названия до двоеточия («xAI: Grok Imagine Image 2.0»); у fal формат id другой. Решить в «Анализе»; группировка и имя группы — в `Presentation`, не в оболочках.

Зависит от: 090 (те же файлы экрана моделей).
Среда: Linux или Windows (как у 090).

## Критерии готовности
- Модели разбиты на группы по производителю; группы по алфавиту, внутри группы по названию.
- Группа раскрывается и сворачивается; в заголовке имя производителя и число моделей (для картинок и число выбранных).
- При открытии экрана раскрыты группы с выбранными моделями; при поиске видны только группы с совпадениями, и они раскрыты.
- Модели без понятного производителя попадают в группу «Other».
- Тесты `Presentation` на группировку (fal, OpenRouter, без префикса) и на поиск; тест Avalonia на заголовки групп.

## Анализ
Откуда брать производителя. Живые каталоги проверены без ключа 2026-10-29:
- OpenRouter, картинки (`GET /api/v1/images/models`, 61 модель). У всех id есть префикс до `/`, у всех название вида «Производитель: Модель». Подписи у одного префикса могут различаться: у `x-ai` есть и «xAI: …», и «SpaceXAI: …».
- OpenRouter, чат с `tools` (391 модель). Есть id-псевдонимы с `~` (`~anthropic/…`). Два префикса дают одного производителя: `meta` и `meta-llama` → «Meta». 11 моделей без «Производитель:» в названии (`openrouter/auto` «Auto Router», `mistralai/mistral-large` «Mistral Large»).
- fal: id вида `fal-ai/flux/schnell`, но встречается и `openai/gpt-image-2`. `fal-ai` — пространство имён fal, а не производитель. Поля производителя в `metadata` нет; в названиях нет «Производитель:».
- Модели OpenAI-совместимых провайдеров из конфига могут быть без `/`.

Решения плана (допущения из комментариев claude-plan, приняты в плане):
1. Префикс — часть id до первого `/` после удаления ведущих `~`. Нет `/` или `/` стоит первым символом → производитель неизвестен.
2. У провайдера `fal` префикс `fal-ai` → производитель неизвестен.
3. Подпись группы префикса — самая частая подпись среди её моделей (часть названия до первого `": "`). При равенстве берётся первая по `StringComparer.OrdinalIgnoreCase`: для `x-ai` это «SpaceXAI». Если ни у одной модели префикса нет `": "`, подпись — сам префикс.
4. Группы с одинаковой подписью без учёта регистра сливаются (`meta` + `meta-llama` → «Meta»). Подпись общей группы — меньшая по `StringComparer.Ordinal`. Модели разных провайдеров одного производителя попадают в одну группу.
5. Неизвестный производитель → группа «Other».
6. Порядок: группы по подписи (`OrdinalIgnoreCase`), «Other» всегда последняя. Внутри группы — по названию (`OrdinalIgnoreCase`), затем по id провайдера (`Ordinal`).
7. Раскрытие:
   - При открытии раскрыта группа, в которой есть выбранная модель.
   - Нажатие без поиска запоминается.
   - При непустом поиске видны только группы с совпадениями, все раскрыты. Нажатие во время поиска меняет только показ и не запоминается.
   - После очистки поиска возвращаются запомненные состояния.
   - «Refresh» запомненные состояния не меняет.
   - Новая группа (без запомненного состояния) раскрыта, если в ней есть выбранная модель.
8. Свёрнутая группа держит пустой список строк, чтобы MAUI (`BindableLayout`) не создавал лишние представления: у fal моделей сотни.
9. Заголовок для картинок — «Black Forest Labs (6) · 1 selected», для ассистента — «Anthropic (30)». Число моделей — совпадающие с поиском; число выбранных — отмеченные среди них.
10. MAUI: заголовок группы — кнопка `ghost` со значком (как «All models» на `ModelPickerPage`). Avalonia: `Expander` (как «All models» в `ModelPickerView`).
11. Плоские `Models` и `AssistantModels` остаются: их читают тесты. Разметка переходит на группы.
12. Экран «Other model…» (`ModelPickerPageModel`) не меняется.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, членов, ключей строк и тестов переноси дословно. При расхождении с текущим API адаптируй имена и разметку в рамках описанного контракта и проверь сборкой. `blocked` нужен только для неизвестного продуктового решения или недоступной обязательной среды; ошибку компиляции исправляй минимальным изменением, без расширения задачи.

Выполнять после: 090 (те же `SessionModelsPageModel`, `ImageGen.Maui/Controls/SessionModelsView.xaml`, `ImageGen.Avalonia/Views/SessionModelsView.axaml`). Если 090 ещё не `done` в main, задачу не занимать. 091 снята, её критерии перенесены в 090.

Прочитай перед началом:
- «Анализ» выше (правила 1–12).
- `ImageGen/ImageGen.Presentation/PageModels/SessionModelsPageModel.cs` (после 090), `ModelOptionViewModel.cs`, `AssistantModelOptionViewModel.cs` (образец делегата `select`).
- `ImageGen/ImageGen.Presentation/Appearance/IconGlyphs.cs` (`Expand`, `Forward`).
- `ImageGen/ImageGen.Maui/Pages/ModelPickerPage.xaml` (кнопка «All models» и `IsVisible`) и `ImageGen/ImageGen.Avalonia/Views/ModelPickerView.axaml` (`Expander`).
- `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionModelsPageModelTests.cs`. Тестовый каталог: Alpha `vendor/alpha`, «Beta Pro» `vendor/beta` (сохранена выбранной), Gamma `other/gamma`. Ассистенты: «Zed» `vendor/zed` и «Assistant» `vendor/assistant` (сохранён).
- `docs/knowledge/maui.md` (BindableLayout и длинные списки), `docs/specs/chat-sessions.md` S5.

1. Строки (в `AppResources.resx` и свойства в `AppResources.cs`):
   - `ModelGroupOther` = «Other»;
   - `ModelGroupImageHeaderFormat` = «{0} ({1}) · {2} selected»;
   - `ModelGroupAssistantHeaderFormat` = «{0} ({1})»;
   - `ModelGroupExpanded` = «expanded»;
   - `ModelGroupCollapsed` = «collapsed».
2. Группировка (сначала тесты). Новый файл `ImageGen/ImageGen.Presentation/PageModels/ModelVendors.cs`:
   - `public sealed record VendorGroup<T>(string Key, string Label, IReadOnlyList<T> Items);`
   - `public static class ModelVendors`:
     - `public const string OtherKey = "~other";`
     - `public static string? PrefixOf(string providerId, string modelId)` — правила 1–2; результат в нижнем регистре (`ToLowerInvariant`).
     - `public static string? LabelOf(string name)` — часть до первого `": "` с `Trim()`; если `": "` нет или часть пустая — `null`.
     - `public static IReadOnlyList<VendorGroup<T>> Group<T>(IEnumerable<T> items, Func<T, string> providerId, Func<T, string> modelId, Func<T, string> name)` — правила 3–6. `Key` группы — подпись в `ToLowerInvariant`; у «Other» `Key = OtherKey` и `Label = AppResources.ModelGroupOther`.
   - Тесты в новом файле `ImageGen/ImageGen.Presentation.Tests/PageModels/ModelVendorsTests.cs`. Элементы — кортежи `(provider, id, name)`.
     - `Group_OpenRouterName_UsesLabelBeforeColon` — `openrouter`, `black-forest-labs/flux.2-klein-4b`, «Black Forest Labs: FLUX.2 Klein 4B» → одна группа «Black Forest Labs», ключ `black forest labs`.
     - `Group_TwoLabelsTie_TakesFirstAlphabetically` — `x-ai/a` «xAI: A» и `x-ai/b` «SpaceXAI: B» → одна группа «SpaceXAI» с двумя моделями.
     - `Group_TildeAlias_JoinsVendor` — `~anthropic/claude-latest` «Anthropic: Claude Latest» и `anthropic/claude-x` «Anthropic: Claude X» → одна группа «Anthropic».
     - `Group_TwoPrefixesSameLabel_Merge` — `meta/x` «Meta: X» и `meta-llama/y` «Meta: Y» → одна группа «Meta».
     - `Group_NoLabelInPrefix_UsesPrefix` — `openrouter/auto` «Auto Router» → группа «openrouter».
     - `Group_NameWithoutLabel_JoinsPrefixLabel` — `mistralai/mistral-large` «Mistral Large» и `mistralai/small` «Mistral: Small» → одна группа «Mistral».
     - `Group_FalNamespace_IsOther` — `fal`, `fal-ai/flux/dev`, «FLUX.1 [dev]» → группа с `Key = OtherKey` и подписью «Other».
     - `Group_FalVendorPrefix_JoinsOpenRouterVendor` — `fal`, `openai/gpt-image-2`, «GPT Image 2 API» и `openrouter`, `openai/gpt-image-1`, «OpenAI: GPT Image 1» → одна группа «OpenAI» с двумя моделями.
     - `Group_IdWithoutSlash_IsOther` — `local`, `dall-e-3`, «DALL-E 3» → «Other».
     - `Group_Order_AlphabeticalOtherLast` — группы «Zeta», «alpha», «Other» → порядок «alpha», «Zeta», «Other»; внутри группы «b» и «A» → «A», «b».
     - `Group_Empty_ReturnsEmpty`.
3. Модели групп. Новые файлы в `ImageGen/ImageGen.Presentation/PageModels/`.
   - `ModelGroupViewModel.cs`: `public abstract partial class ModelGroupViewModel : ObservableObject`, конструктор `(string key, string name, bool isExpanded, Action<ModelGroupViewModel> expandedChanged)`. Члены:
     - `string Key`, `string Name`;
     - `[ObservableProperty] private bool _isExpanded;` (обычный backing field, generator создаёт `IsExpanded`). В базовом конструкторе присвоить непосредственно `_isExpanded = isExpanded`, не свойство. В конструкторе нельзя вызывать виртуальный `FillRows` и delegate: `Rows` производного класса ещё не инициализирована. Производные конструкторы сначала присваивают `Matches`, создают/инициализируют `Rows`, затем один раз вызывают `FillRows`. `partial void OnIsExpandedChanged(bool value)` вызывает `FillRows()`, затем `expandedChanged(this)` и уведомляет `AutomationName` и `ChevronGlyph`;
     - `[RelayCommand] private void Toggle()` — `IsExpanded = !IsExpanded`;
     - `public abstract string HeaderText { get; }`;
     - `public string AutomationName` — `"{HeaderText}, {ModelGroupExpanded}"` или `"{HeaderText}, {ModelGroupCollapsed}"`;
     - `public string ChevronGlyph` — `IconGlyphs.Expand`, если раскрыта, иначе `IconGlyphs.Forward`;
     - `protected abstract void FillRows()`.
   - В новом `ModelGroupViewModelTests.cs` регрессии создания: раскрытая image-группа сразу имеет строки и не вызывает callback; свёрнутая группа пуста; первый Toggle заполняет строки и вызывает callback ровно один раз. Те же случаи для assistant-группы.
   - `ImageModelGroupViewModel.cs`: `public sealed class ImageModelGroupViewModel : ModelGroupViewModel`, конструктор `(string key, string name, IReadOnlyList<ModelOptionViewModel> matches, bool isExpanded, Action<ModelGroupViewModel> expandedChanged)`. Члены:
     - `IReadOnlyList<ModelOptionViewModel> Matches`;
     - `ObservableCollection<ModelOptionViewModel> Rows` — заполняется в конструкторе и в `FillRows()`: `Matches`, если раскрыта, иначе пусто;
     - `int SelectedCount` — число `Matches` с `IsSelected`;
     - `HeaderText` — `string.Format(CultureInfo.CurrentCulture, AppResources.ModelGroupImageHeaderFormat, Name, Matches.Count, SelectedCount)`;
     - `public void NotifySelectionChanged()` — `OnPropertyChanged` для `SelectedCount`, `HeaderText`, `AutomationName`. На строки не подписывается.
   - `AssistantModelGroupViewModel.cs`: устроена так же, но с `AssistantModelOptionViewModel`, без `SelectedCount`; `HeaderText` — по `ModelGroupAssistantHeaderFormat` (`Name`, `Matches.Count`).
4. Группы в `SessionModelsPageModel` (сначала тесты).
   - Новые члены:
     - `public ObservableCollection<ImageModelGroupViewModel> ImageModelGroups { get; } = [];`
     - `public ObservableCollection<AssistantModelGroupViewModel> AssistantModelGroups { get; } = [];`
     - приватные `Dictionary<string, bool> _imageGroupExpanded` и `_assistantGroupExpanded` (`StringComparer.Ordinal`).
   - В конце `ApplyFilter` перестрой обе коллекции через `ModelVendors.Group` из уже отфильтрованных `Models` и `AssistantModels`. Провайдер — `Reference.ProviderId`, id — `Reference.ModelId`, название — `Name`.
   - `isExpanded` для каждой группы:
     - если поиск непустой (`SearchText.Trim().Length > 0`) → `true`;
     - иначе, если ключа нет в словаре, записать в словарь: картинки — есть ли в группе строка с `IsSelected`, ассистент — есть ли строка с `IsSelected`; затем брать значение из словаря.
   - Делегат `expandedChanged`: если поиск пустой — записать `IsExpanded` группы в словарь по ключу; если непустой — ничего.
   - В существующем обработчике `PropertyChanged` строки картинок (в `LoadAsync`, там же, где `OnPropertyChanged(nameof(SelectedCountText))`) вызывай `NotifySelectionChanged()` у всех групп из `ImageModelGroups`.
   - Тесты в `SessionModelsPageModelTests`:
     - `OnAppearingAsync_GroupWithSavedModel_IsExpanded` — `ImageModelGroups` = группы «other» и «vendor» (в этом порядке); у «vendor» `IsExpanded = true` и `Rows` = Alpha, «Beta Pro»; у «other» `IsExpanded = false` и `Rows` пуст.
     - `ToggleCommand_CollapsedGroup_FillsRows` — `ToggleCommand` у «other» → `IsExpanded = true`, `Rows` = Gamma.
     - `IsSelected_Changed_UpdatesGroupHeader` — отметить Alpha → `HeaderText` группы «vendor» = «vendor (2) · 2 selected».
     - `SearchText_ShowsOnlyMatchingGroupsExpanded` — поиск «gamma» → одна группа «other», раскрыта, `Rows` = Gamma.
     - `SearchText_Cleared_RestoresRememberedState` — свернуть «vendor», искать «a», очистить поиск → «vendor» свёрнута, «other» свёрнута.
     - `ToggleWhileSearching_DoesNotChangeRememberedState` — поиск «alpha», свернуть «vendor», очистить → «vendor» раскрыта (как было до поиска).
     - `RefreshCommand_KeepsGroupExpandState` — раскрыть «other», `RefreshCommand` → «other» раскрыта.
     - `OnAppearingAsync_AssistantGroupWithSavedAssistant_IsExpanded` — `AssistantModelGroups` = одна группа «vendor» (2), раскрыта.
     - Все существующие тесты зелёные.
5. Avalonia, разметка `ImageGen/ImageGen.Avalonia/Views/SessionModelsView.axaml`.
   - Вкладка картинок: `ItemsControl` внутри `ScrollViewer` получает `ItemsSource="{Binding ImageModelGroups}"`. Шаблон группы (`DataTemplate`, `x:DataType="pm:ImageModelGroupViewModel"`) — `Expander`:
     - `Header="{Binding HeaderText}"`, `IsExpanded="{Binding IsExpanded, Mode=TwoWay}"`, `AutomationProperties.Name="{Binding AutomationName}"`;
     - содержимое — `ItemsControl ItemsSource="{Binding Rows}"` с прежним шаблоном строки `ModelOptionViewModel`.
   - Вкладка ассистента: так же, с `AssistantModelGroups`, `AssistantModelGroupViewModel` и прежним шаблоном строки ассистента (с `GroupName="assistant"`).
   - Тесты в `SessionModelsFlowTests` (каталог заглушки не меняется: `black-forest-labs/flux.2-klein-4b` «FLUX.2 Klein 4B» выбрана по умолчанию, `vendor/beta` «Beta Pro»):
     - `ImageGroups_HeadersShowVendorAndCounts` — два `Expander` с заголовками «black-forest-labs (1) · 1 selected» и «vendor (1) · 0 selected»; первый раскрыт, второй свёрнут.
     - `ImageGroups_ExpandShowsCheckBoxes` — `IsExpanded = true` у второго → после `UpdateLayout()` внутри него есть `CheckBox` с `AutomationProperties.Name` «Beta Pro».
     - `ViewsAccessibilityTests` зелёный.
6. MAUI, разметка `ImageGen/ImageGen.Maui/Controls/SessionModelsView.xaml`.
   - Вкладка картинок: внутри `ScrollView` — `VerticalStackLayout` с `BindableLayout.ItemsSource="{Binding ImageModelGroups}"`. Шаблон группы (`x:DataType="pm:ImageModelGroupViewModel"`) — `VerticalStackLayout`:
     - `Button StyleClass="ghost"` с `HorizontalOptions="Fill"`, `Text="{Binding HeaderText}"`, `Command="{Binding ToggleCommand}"`, `SemanticProperties.Description="{Binding AutomationName}"`;
     - у кнопки `ImageSource` — `FontImageSource` с `FontFamily="LucideIcons"`, `Glyph="{Binding ChevronGlyph}"`, `Color="{DynamicResource IgFgColor}"`, `Size="16"`;
     - под кнопкой `VerticalStackLayout` с `BindableLayout.ItemsSource="{Binding Rows}"` и прежним шаблоном строки `ModelOptionViewModel`.
   - Вкладка ассистента: так же, с `AssistantModelGroups`. `RadioButtonGroup.GroupName` (или запасной `GroupName` из 090) поставь на внешний `VerticalStackLayout` групп ассистента.
   - Сборка Android.
7. Документация: в `docs/specs/chat-sessions.md`, раздел S5, добавь пункт S5.5: модели на обеих вкладках сгруппированы по производителю; перечисли правила 1–9 из «Анализа» коротко, по одной строке.
8. Ручные пункты (см. «Ручная проверка») допиши в задачи 016 и 046.

После проверок заполни «Отчёт», перечисли доступные и отложенные ручные проверки, выполни `scripts/release-task.sh 092`, затем `bash scripts/check.sh`. Отсутствие Windows/Android-устройства для ручной проверки не блокирует завершение: пункты передаются владельцу по `docs/tasks.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

### Ручная проверка
- В задачу 016 (MAUI под Windows): после «Refresh» модели разбиты по производителям, группа с выбранной моделью раскрыта, нажатие на заголовок сворачивает; поиск «flux» оставляет только группы с совпадениями, раскрытыми; диктор читает «…, expanded/collapsed»; выбор ассистента внутри групп единственный.
- В задачу 046 (Avalonia): то же; заголовок группы раскрывается с клавиатуры (Enter или пробел).
- Android — в «Отчёт» этой задачи: «не проверено, проверить владельцу: группы на телефоне, скорость раскрытия большой группы».

Файлы: `ImageGen/ImageGen.Presentation/PageModels/ModelVendors.cs`, `ImageGen/ImageGen.Presentation/PageModels/ModelGroupViewModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/ImageModelGroupViewModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/AssistantModelGroupViewModel.cs` (новые), `ImageGen/ImageGen.Presentation/PageModels/SessionModelsPageModel.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.cs`, `ImageGen/ImageGen.Maui/Controls/SessionModelsView.xaml`, `ImageGen/ImageGen.Avalonia/Views/SessionModelsView.axaml`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ModelVendorsTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ModelGroupViewModelTests.cs` (новые), `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionModelsPageModelTests.cs`, `ImageGen/ImageGen.Avalonia.Tests/Flows/SessionModelsFlowTests.cs`, `docs/specs/chat-sessions.md`, `tasks/v0.1/016-*.md`, `tasks/backlog/046-*.md`.

## Отчёт

## Комментарии
### 2026-10-29, claude-plan
Вопросы владельцу по плану (до ответа план идёт по допущениям):
1. Модели fal с id `fal-ai/…` (FLUX, Nano Banana и др.) — производителя в каталоге fal нет. Допущение: группа «Other». Варианты: отдельная группа «fal.ai» или ручная таблица семейств (`flux` → Black Forest Labs и т. п.).
2. Группа «Other» — последней, а не по алфавиту (допущение).
3. Подпись группы берётся из названий моделей OpenRouter; у `x-ai` сейчас встречаются «xAI» и «SpaceXAI» — при равенстве выбирается первая по алфавиту («SpaceXAI»). Подходит?

### 2026-10-29, владелец (в чате, записал claude-plan)
План одобрен; допущения из комментариев claude-plan приняты как решения. План переписан под небольшую модель.
