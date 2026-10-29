---
id: 090
status: approved
assignee:
claimed:
created: 2026-10-29
---

# Модели сессии: вкладки «Картинки» и «Ассистент»

## Описание
Замечание владельца (2026-10-29): на экране моделей сессии трудно ориентироваться: длинный список моделей картинок, под ним длинный список моделей ассистента, всё в одной прокрутке. Нужны вкладки «Image models» и «Assistant model», у каждой свой список и своя прокрутка.

Экран есть в двух местах каждой оболочки: страница (`SessionModelsPage` в MAUI, `SessionModelsView` в Avalonia) и боковая панель широкого окна (071; в MAUI это копия разметки в `ChatPage.xaml`, в Avalonia тот же `SessionModelsView`). Вкладки нужны везде. В «Анализе» оценить вынос общей разметки MAUI в `ContentView`, чтобы последующие задачи не правили две копии.

Зависит от: —
Среда: Linux или Windows (Avalonia — headless-тесты на Linux; MAUI — сборка Android на Linux, Windows-цель собирает CI; ручные проверки — 016 и 046).

## Критерии готовности
- Две вкладки с подписями из `AppResources`; по умолчанию открыта «Image models».
- Поиск фильтрует список открытой вкладки (или обеих: решение записать в «Анализе»); счётчик выбранных моделей картинок виден на своей вкладке.
- Выбор на одной вкладке не теряется при переключении на другую и сохраняется общей кнопкой «Save».
- Вкладки доступны с клавиатуры и для экранного диктора (`SemanticProperties`, `AutomationProperties`).
- Спецификация экрана моделей в `docs/specs/` обновлена; тест Avalonia на наличие вкладок.
- «Save» и «Cancel» на странице и в панели моделей находятся в отдельной `Auto`-строке вне вкладок и прокрутки; остаются видимыми при длинном каталоге на обеих вкладках. Параметры уже устроены так — их разметку не менять.

## Анализ
Как сейчас (проверено по коду 2026-10-29):
- `SessionModelsPageModel` держит два плоских отфильтрованных списка: `Models` (модели картинок с галочками) и `AssistantModels` (ассистент, один выбор через `SelectAssistant`). `SearchText` фильтрует оба списка (`ApplyFilter`). Выбор хранится в `_all` и `_assistants`, а не в отображаемых списках, поэтому при переключении вкладок он не теряется.
- MAUI: страница `Pages/SessionModelsPage.xaml` и полная копия её разметки в `Pages/ChatPage.xaml` (комментарий «Models pane body (mirrors SessionModelsPage)»). У копии своя группа переключателей `paneAssistant` и свой обработчик `OnAssistantCheckedChanged` в `ChatPage.xaml.cs`; порядка кнопок для macOS нет; кнопки внутри `ScrollView`.
- Avalonia: один `Views/SessionModelsView.axaml`, панель в `ChatView.axaml` показывает его через `DataTemplate`.
- Модель страницы создаётся заново при каждом открытии (`Func<SessionModelsPageModel>` в DI), поэтому вкладка по умолчанию задаётся просто начальным значением.

Решения плана (допущения из комментариев claude-plan, приняты в плане):
- В MAUI нет вкладок внутри страницы (`TabbedPage` и вкладки `Shell` работают только на уровне страниц и в боковую панель не встраиваются). Поэтому в MAUI вкладки — две кнопки `ghost` с полоской-подчёркиванием (`BoxView`) под выбранной; содержимое вкладок переключается через `IsVisible`. В Avalonia — `TabControl`: клавиатура и роль вкладки для диктора есть из коробки.
- Состояние вкладки хранится в `Presentation`, разметка его только показывает.
- Поиск: одно поле над вкладками фильтрует обе вкладки, как сейчас.
- Общая разметка MAUI выносится в `ContentView` `ImageGen.Maui/Controls/SessionModelsView.xaml` (образец — `Controls/SessionsListView`). Страница и панель чата только размещают её. После этого 092–093 правят один файл разметки на оболочку.
- Группа переключателей ассистента в MAUI задаётся на контейнере списка: `RadioButtonGroup.GroupName="sessionAssistant"`. Единственность выбора всё равно гарантирует `SelectAssistant`.

Нужность: вкладки и единая MAUI-разметка отсутствуют, задача нужна. Отдельная 091 снята как дублирующая эту работу: закрепление кнопок и его проверки включены сюда. Пересечения: 092 и 093 правят те же файлы, поэтому порядок 090 → 092 → 093. 088, 095, 096, 097 и 098 правят другие участки `ChatPage.xaml`/`ChatPage.xaml.cs`, а 095 — ещё и `SessionModelsPage.xaml` (отступы от клавиатуры). При конфликте во время rebase сохраняй обе стороны.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, членов, ключей строк и тестов переноси дословно. При расхождении с текущим API адаптируй имена и разметку в рамках описанного контракта и проверь сборкой. `blocked` нужен только для неизвестного продуктового решения или недоступной обязательной среды; ошибку компиляции исправляй минимальным изменением, без расширения задачи.

Выполнять после: 095, если она уже в работе (она меняет `SessionModelsPage.xaml`). При конфликте сохраняй обе правки.

Прочитай перед началом:
- `ImageGen/ImageGen.Presentation/PageModels/SessionModelsPageModel.cs`, `AssistantModelOptionViewModel.cs`.
- `ImageGen/ImageGen.Maui/Pages/SessionModelsPage.xaml(.cs)`; в `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml` блок от комментария «Models pane body (mirrors SessionModelsPage)» до комментария «Parameters pane body»; в `ChatPage.xaml.cs` — `OnAssistantCheckedChanged`.
- `ImageGen/ImageGen.Maui/Controls/SessionsListView.xaml(.cs)` (образец `ContentView`).
- `ImageGen/ImageGen.Avalonia/Views/SessionModelsView.axaml(.cs)`, `ImageGen/ImageGen.Avalonia/Views/ModelPickerView.axaml`.
- `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.cs` (свойства пишутся вручную на каждую строку `.resx`).
- `ImageGen/ImageGen.Presentation.Tests/Design/MauiStylesTests.cs` (как тест читает файлы MAUI).
- `docs/knowledge/maui.md` (раздел про переключатель ассистента, задача 070), `docs/knowledge/avalonia.md` (XAML), `docs/specs/chat-sessions.md` S5.

1. Тест-сторож разметки MAUI (сначала падает). Новый файл `ImageGen/ImageGen.Presentation.Tests/Design/MauiSessionModelsMarkupTests.cs`, класс `MauiSessionModelsMarkupTests`. Путь к папке MAUI находи так же, как `MauiStylesTests` (тот же приём `MauiRoot()`; если он приватный — скопируй его в новый класс). Тесты:
   - `SessionModelsView_Exists` — файл `Controls/SessionModelsView.xaml` существует.
   - `SessionModelsPage_UsesSharedView` — текст `Pages/SessionModelsPage.xaml` содержит `controls:SessionModelsView` и не содержит `{Binding AssistantModels}`.
   - `ChatPage_UsesSharedView` — текст `Pages/ChatPage.xaml` содержит `controls:SessionModelsView` и не содержит `{Binding AssistantModels}`.
2. Вынос в `ContentView` (рефакторинг без смены поведения).
   - Новые файлы `ImageGen/ImageGen.Maui/Controls/SessionModelsView.xaml` и `.xaml.cs`: `ContentView`, `x:Class="ImageGen.Maui.Controls.SessionModelsView"`, `x:DataType="pm:SessionModelsPageModel"`, те же xmlns, что у `SessionModelsPage.xaml`. Содержимое — корневой `Grid` из `SessionModelsPage.xaml` целиком (с `Padding="16"`), включая `ButtonRow`, `SaveButton` и `CancelButton` с их `x:Name`.
   - У `RadioButton` убери атрибут `GroupName`; на `VerticalStackLayout` с `BindableLayout.ItemsSource="{Binding AssistantModels}"` поставь `RadioButtonGroup.GroupName="sessionAssistant"`.
   - В `SessionModelsView.xaml.cs` перенеси из `SessionModelsPage.xaml.cs` методы `ApplyButtonOrder` (вызов в конструкторе после `InitializeComponent`) и `OnAssistantCheckedChanged`.
   - `SessionModelsPage.xaml`: содержимое страницы — только `<controls:SessionModelsView />` (добавь `xmlns:controls="clr-namespace:ImageGen.Maui.Controls"`). В `SessionModelsPage.xaml.cs` удали `ApplyButtonOrder` и `OnAssistantCheckedChanged`; `ApplyQueryAttributes` и `OnAppearing` оставь.
   - `ChatPage.xaml`: весь `ScrollView` после комментария «Models pane body (mirrors SessionModelsPage)» замени на `controls:SessionModelsView` с `Grid.Row="1"` и теми же `IsVisible` и `BindingContext="{Binding ModelsPane}"`, что были у `ScrollView`; комментарий поменяй на «Models pane body (shared Controls/SessionModelsView)». Из `ChatPage.xaml.cs` удали `OnAssistantCheckedChanged`.
   - Проверка: тесты шага 1 зелёные, сборка Android проходит.
   - Если сборка падает на `RadioButtonGroup.GroupName`, верни `GroupName="assistant"` на `RadioButton` и запиши это в «Отчёт».
3. Состояние вкладок (сначала тесты). Новый файл `ImageGen/ImageGen.Presentation/PageModels/SessionModelsTab.cs`: `public enum SessionModelsTab { ImageModels = 0, AssistantModel = 1 }`.
   - Строка: ключ `ImageModelsTab`, текст «Image models» (в `AppResources.resx` и свойство `public static string ImageModelsTab` в `AppResources.cs`). Для второй вкладки бери существующий `AssistantModelTitle` («Assistant model»), для «selected» — существующий `AssistantSelected`.
   - Члены `SessionModelsPageModel`:
     - `[ObservableProperty] public partial SessionModelsTab SelectedTab { get; set; } = SessionModelsTab.ImageModels;` с `[NotifyPropertyChangedFor]` для `IsImageModelsTabSelected`, `IsAssistantTabSelected`, `SelectedTabIndex`, `ImageModelsTabAutomationName`, `AssistantTabAutomationName`.
     - `public bool IsImageModelsTabSelected` и `public bool IsAssistantTabSelected` — сравнение `SelectedTab`.
     - `public int SelectedTabIndex { get; set; }`: get — `(int)SelectedTab`; set — значение 0 или 1 ставит соответствующую вкладку, любое другое игнорируется.
     - `public string ImageModelsTabAutomationName` — `ImageModelsTab`, а у выбранной вкладки `"{ImageModelsTab}, {AssistantSelected}"`. Так же устроен `AssistantTabAutomationName` с `AssistantModelTitle`.
     - `[RelayCommand] private void ShowImageModels()` и `[RelayCommand] private void ShowAssistantModel()` — ставят вкладку.
   - Тесты в `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionModelsPageModelTests.cs`:
     - `SelectedTab_NewPageModel_IsImageModels` — `SelectedTab = ImageModels`, `IsImageModelsTabSelected = true`, `SelectedTabIndex = 0`.
     - `ShowAssistantModelCommand_SwitchesTabAndNotifies` — после команды `SelectedTab = AssistantModel`, флаги поменялись; `PropertyChanged` пришёл для `IsImageModelsTabSelected`, `IsAssistantTabSelected`, `SelectedTabIndex`.
     - `SelectedTabIndex_One_SelectsAssistantTab` — `SelectedTab = AssistantModel`.
     - `SelectedTabIndex_OutOfRange_KeepsTab` — значение 5 оставляет `ImageModels`.
     - `SwitchTabs_KeepsSelectionOfBothTabs` — после загрузки отметить Alpha, переключиться на ассистента, выполнить `SelectCommand` у строки «Zed», вернуться на картинки; затем `SaveCommand` сохраняет модели Alpha и Beta (вместе с неизвестной сохранённой) и ассистента `vendor/zed`.
     - `SearchText_OnAssistantTab_FiltersBothLists` — на вкладке ассистента поиск «zed» даёт пустой `Models` и одну строку в `AssistantModels`.
     - `TabAutomationNames_MarkOnlySelectedTab` — у вкладки картинок есть «selected», у вкладки ассистента нет; после переключения наоборот.
4. Avalonia: вкладки (сначала тесты). В `ImageGen/ImageGen.Avalonia.Tests/Flows/SessionModelsFlowTests.cs`:
   - `Tabs_TwoTabsWithImageModelsFirst` — открыть экран моделей (как в существующих тестах), `Window.Show()`, `UpdateLayout()`. В представлении один `TabControl`, у него два `TabItem` с `Header`, равными `AppResources.ImageModelsTab` и `AppResources.AssistantModelTitle`, `SelectedIndex = 0`.
   - `Tabs_SwitchingTabItem_ChangesPageModelTab` — `TabControl.SelectedIndex = 1` даёт `SelectedTab = AssistantModel`; `ShowImageModelsCommand` возвращает `SelectedIndex = 0`.
   - `Tabs_EachTabHasOwnScrollViewer_ButtonsOutsideTabs` — у каждого `TabItem` есть потомок `ScrollViewer`; у `SaveButton` и `CancelButton` нет предка `TabControl`.
   - Разметка `ImageGen/ImageGen.Avalonia/Views/SessionModelsView.axaml`:
     - корневой `Grid` — `RowDefinitions="Auto,Auto,*,Auto"`: строка 0 — поиск и справа кнопка Refresh (перенеси её из старой строки со счётчиком); строка 1 — `ProgressBar`; строка 2 — `TabControl` с `SelectedIndex="{Binding SelectedTabIndex, Mode=TwoWay}"`; строка 3 — `ButtonRow` без изменений.
     - Первый `TabItem`: `Header="{markup:Res ImageModelsTab}"`, `AutomationProperties.Name="{markup:Res ImageModelsTab}"`. Внутри `Grid RowDefinitions="Auto,Auto,*"`: панель с числом запусков (старая строка 1), `TextBlock.section` со `SelectedCountText`, `ScrollViewer` со списком `Models` (старый `ItemsControl`).
     - Второй `TabItem`: `Header="{markup:Res AssistantModelTitle}"`, `AutomationProperties.Name` так же; внутри `ScrollViewer` со старым `ItemsControl` для `AssistantModels`. Заголовок-`TextBlock` «Assistant model» внутри прокрутки удали.
     - В `SessionModelsView.axaml.cs` ничего не меняется.
   - `ImageGen/ImageGen.Avalonia.Tests/Views/ChatPaneTests.cs`: тест `Wide_ModelsPane_HasTabs` — в широкой панели есть `TabControl` с двумя `TabItem`.
   - `ViewsAccessibilityTests.Views_InteractiveControls_HaveAutomationName` должен остаться зелёным. Если он падает на `TabItem`, проверь, что у обоих `TabItem` задан `AutomationProperties.Name`.
5. MAUI: вкладки в `ImageGen/ImageGen.Maui/Controls/SessionModelsView.xaml`.
   - Корневой `Grid` — `RowDefinitions="Auto,Auto,Auto,*,Auto"`.
     - Строка 0: `Grid ColumnDefinitions="*,Auto"` — `SearchBar` и справа кнопка Refresh (перенеси из старой строки со счётчиком).
     - Строка 1: `ActivityIndicator`.
     - Строка 2: полоса вкладок.
     - Строка 3: содержимое вкладок.
     - Строка 4: `ButtonRow`.
   - Полоса вкладок: `Grid ColumnDefinitions="*,*" RowDefinitions="Auto,2"`. В строке 0 две `Button StyleClass="ghost"`:
     - первая: `Text="{x:Static strings:AppResources.ImageModelsTab}"`, `Command="{Binding ShowImageModelsCommand}"`, `SemanticProperties.Description="{Binding ImageModelsTabAutomationName}"`;
     - вторая: `AssistantModelTitle`, `ShowAssistantModelCommand`, `AssistantTabAutomationName`.
   - В строке 1 под каждой кнопкой `BoxView` с `Color="{DynamicResource IgAccentColor}"` и `IsVisible="{Binding IsImageModelsTabSelected}"` (у второй — `IsAssistantTabSelected`).
   - Содержимое вкладок (строка 3):
     - `Grid RowDefinitions="Auto,Auto,*"` с `IsVisible="{Binding IsImageModelsTabSelected}"`: число запусков, `Label.section` со `SelectedCountText`, `ScrollView` со списком `Models`.
     - В той же строке `ScrollView` с `IsVisible="{Binding IsAssistantTabSelected}"` и списком `AssistantModels` (контейнер с `RadioButtonGroup.GroupName` сохраняется). Заголовок «Assistant model» внутри прокрутки удали.
   - Сборка Android.
6. Закреплённые кнопки (требования снятой 091).
   - В `MauiSessionModelsMarkupTests` проверять XAML через `XDocument`: у `SaveButton`/`CancelButton` нет предков `ScrollView`, а `ButtonRow` — прямой потомок корневого Grid в строке `Auto`; строка содержимого имеет размер `*`. Проверять общий control, который используют страница и панель.
   - В `SessionModelsFlowTests` добавить `LongCatalog_SaveAndCancelStayVisibleOnBothTabs`: каталог из 60 моделей, узкое окно 600×640; переключить обе вкладки, выполнить layout и убедиться, что кнопки видимы и целиком попадают в bounds представления (перевод координат `TranslatePoint`), предков ScrollViewer нет. Ответ длинного каталога задавать в локальном StubHttpMessageHandler теста, без изменения всех fakes.
   - В `ChatPaneTests` широкая панель моделей: у обеих кнопок нет предков ScrollViewer; размещение control находится в ограниченной `*`-строке, не внутри внешней прокрутки. Разметку экранов параметров не менять.
7. Документация.
   - `docs/specs/chat-sessions.md`, раздел S5: после S5.2 добавь пункт S5.3: экран моделей сессии (страница и боковая панель) — две вкладки «Image models» (открыта по умолчанию: число запусков, счётчик выбранных, список с галочками) и «Assistant model» (список с одним выбором), у каждой своя прокрутка; поле поиска и «Refresh» над вкладками, поиск фильтрует обе вкладки; «Save» и «Cancel» общие, закреплены под вкладками вне прокрутки и видны при длинном списке.
   - `docs/architecture.md`, строка `ImageGen.Maui/`: в существующий список `Controls/ (SessionsListView, StarterExamples, ChatEditor)` добавь `SessionModelsView` (общий экран моделей страницы и панели); не дублируй `Controls/` и не удаляй `ChatEditor`.
   - `docs/knowledge/maui.md`, пункт «Переключатель модели ассистента в MAUI (задача 070)»: допиши, что разметка экрана моделей общая (`Controls/SessionModelsView`), обработчик `CheckedChanged` и порядок кнопок — в её коде, группа задаётся `RadioButtonGroup.GroupName` на контейнере (или `GroupName`, если сработал запасной путь шага 2).
8. Ручные пункты (см. «Ручная проверка») допиши в задачи 016 и 046.

После проверок заполни «Отчёт», перечисли доступные и отложенные ручные проверки, выполни `scripts/release-task.sh 090`, затем `bash scripts/check.sh`. Отсутствие Windows/Android-устройства для ручной проверки не блокирует завершение: пункты передаются владельцу по `docs/tasks.md`.

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
Windows-цель MAUI собирает CI.

### Ручная проверка
- В задачу 016 (MAUI под Windows): на странице моделей (узкое окно) и в боковой панели (широкое окно) две вкладки, по умолчанию «Image models»; переключение не сбрасывает галочки и выбор ассистента; «Save» сохраняет оба; Tab доходит до вкладок, Enter переключает; диктор читает «Image models, selected».
- В задачу 046 (Avalonia): то же; стрелки влево и вправо переключают вкладки.
- Android — в «Отчёт» этой задачи: «не проверено, проверить владельцу: вкладки на телефоне».

Файлы: `ImageGen/ImageGen.Presentation/PageModels/SessionModelsTab.cs` (новый), `ImageGen/ImageGen.Presentation/PageModels/SessionModelsPageModel.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.cs`, `ImageGen/ImageGen.Maui/Controls/SessionModelsView.xaml`, `ImageGen/ImageGen.Maui/Controls/SessionModelsView.xaml.cs` (новые), `ImageGen/ImageGen.Maui/Pages/SessionModelsPage.xaml`, `ImageGen/ImageGen.Maui/Pages/SessionModelsPage.xaml.cs`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml.cs`, `ImageGen/ImageGen.Avalonia/Views/SessionModelsView.axaml`, `ImageGen/ImageGen.Presentation.Tests/Design/MauiSessionModelsMarkupTests.cs` (новый), `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionModelsPageModelTests.cs`, `ImageGen/ImageGen.Avalonia.Tests/Flows/SessionModelsFlowTests.cs`, `ImageGen/ImageGen.Avalonia.Tests/Views/ChatPaneTests.cs`, `docs/specs/chat-sessions.md`, `docs/architecture.md`, `docs/knowledge/maui.md`, `tasks/v0.1/016-*.md`, `tasks/backlog/046-*.md`.

## Отчёт

## Комментарии

### 2026-10-29, владелец (в чате, записал claude-plan)
План одобрен; допущения из комментариев claude-plan приняты как решения. План переписан под небольшую модель.
