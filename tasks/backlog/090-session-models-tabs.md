---
id: 090
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Модели сессии: вкладки «Картинки» и «Ассистент»

## Описание
Замечание владельца (2026-10-29): на экране моделей сессии трудно ориентироваться: длинный список моделей картинок, под ним длинный список моделей ассистента, всё в одной прокрутке. Нужны вкладки «Image models» и «Assistant model», у каждой свой список и своя прокрутка.

Экран есть в двух местах каждой оболочки: страница (`SessionModelsPage` в MAUI, `SessionModelsView` в Avalonia) и боковая панель широкого окна (071; в MAUI это копия разметки в `ChatPage.xaml`, в Avalonia тот же `SessionModelsView`). Вкладки нужны везде. В «Анализе» оценить вынос общей разметки MAUI в `ContentView`, чтобы 091–093 не правили две копии.

Зависит от: —
Среда: Linux или Windows (Avalonia — headless-тесты на Linux; MAUI — сборка Android на Linux, Windows-цель собирает CI; ручные проверки — 016 и 046).

## Критерии готовности
- Две вкладки с подписями из `AppResources`; по умолчанию открыта «Image models».
- Поиск фильтрует список открытой вкладки (или обеих: решение записать в «Анализе»); счётчик выбранных моделей картинок виден на своей вкладке.
- Выбор на одной вкладке не теряется при переключении на другую и сохраняется общей кнопкой «Save».
- Вкладки доступны с клавиатуры и для экранного диктора (`SemanticProperties`, `AutomationProperties`).
- Спецификация экрана моделей в `docs/specs/` обновлена; тест Avalonia на наличие вкладок.

## Анализ
Как сейчас (проверено по коду 2026-10-29):
- `SessionModelsPageModel` держит два плоских отфильтрованных списка: `Models` (картинки, галочки) и `AssistantModels` (ассистент, один выбор через `SelectAssistant`); `SearchText` фильтрует оба (`ApplyFilter`). Выбор живёт в `_all` и `_assistants`, а не в отображаемых списках, поэтому переключение вкладок выбор не теряет.
- MAUI: страница `Pages/SessionModelsPage.xaml` и полная копия разметки в `Pages/ChatPage.xaml` («Models pane body»). В копии своя группа переключателей (`paneAssistant`), свой обработчик `OnAssistantCheckedChanged` в `ChatPage.xaml.cs`, нет порядка кнопок для macOS и кнопки внутри `ScrollView` (это замечание 091).
- Avalonia: один `Views/SessionModelsView.axaml`; панель в `ChatView.axaml` показывает его же через `DataTemplate`. Копии нет.
- Экземпляр модели страницы создаётся заново при каждом открытии (`Func<SessionModelsPageModel>` в DI обеих оболочек), поэтому «по умолчанию открыта Image models» выполняется сбросом при создании.

Вкладки:
- В MAUI нет вкладок внутри страницы: `TabbedPage` и вкладки `Shell` работают на уровне страниц и в боковую панель не встраиваются. Поэтому в MAUI — полоса из двух кнопок-вкладок и два блока содержимого, видимость по состоянию модели страницы.
- В Avalonia — `TabControl` с двумя `TabItem`: клавиатура (стрелки) и роль вкладки для диктора есть из коробки. `SelectedIndex` привязан к модели страницы, чтобы состояние было одно на обе оболочки и проверялось тестами `Presentation`.
- Состояние вкладки — в `Presentation` (перечисление и команды), разметка только показывает.

Поиск (решение для критерия): одно поле над вкладками фильтрует **обе** вкладки, как сейчас. Причины: фильтр не «прячется» на невидимой вкладке; переключение не сбрасывает ввод; `ApplyFilter` не меняется. Пустой список на другой вкладке при поиске — ожидаемое поведение.

Раскладка: сверху поиск, «Refresh» (обновляет оба каталога) и индикатор загрузки; ниже полоса вкладок; на вкладке «Image models» — число запусков на модель, счётчик «Image models · N selected» и свой прокручиваемый список; на вкладке «Assistant model» — свой прокручиваемый список; внизу, вне вкладок, «Save» и «Cancel».

Вынос общей разметки MAUI в `ContentView` — **делаем в 090 первым шагом** (чистый рефакторинг без смены поведения). Прецедент есть: `Controls/SessionsListView` (`ContentView` с `x:DataType` модели страницы, стоит и на странице сессий, и в чате). Новый `Controls/SessionModelsView` содержит всё тело экрана, включая кнопки; `SessionModelsPage` и панель в `ChatPage` только размещают его. Обработчик переключателя и порядок кнопок (macOS) переезжают в код `ContentView` — дубли в `ChatPage.xaml.cs` и `SessionModelsPage.xaml.cs` уходят. Тогда 091–093 правят по одному файлу разметки на оболочку. Побочный эффект: в панели MAUI кнопки окажутся вне прокрутки уже после 090 (часть 091).

Риск: `RadioButton.GroupName` в MAUI — область видимости группы при двух экземплярах `ContentView` (страница поверх чата) неизвестна, проверить. Допущение: задать группу на контейнере списка (`RadioButtonGroup.GroupName`), выбор всё равно гарантирует модель страницы (`SelectAssistant`).

Пересечения: 091, 092, 093 меняют те же файлы (`SessionModelsPageModel`, разметку экрана моделей в обеих оболочках, `AppResources`) — порядок 090 → 091 → 092 → 093, как в «Описании». Общие файлы волн (`AppResources.resx`/`.cs`, задачи 016 и 046) — только добавление строк.

## План
Типы и члены (`ImageGen.Presentation`):
- `PageModels/SessionModelsTab` — перечисление: `ImageModels`, `AssistantModel`.
- `SessionModelsPageModel`:
  - `SelectedTab` (наблюдаемое, по умолчанию `ImageModels`); производные `IsImageModelsTabSelected`, `IsAssistantTabSelected`, `SelectedTabIndex` (0/1, чтение и запись — для `TabControl` Avalonia; вне диапазона — игнор).
  - `ImageModelsTabAutomationName`, `AssistantTabAutomationName`: подпись вкладки, у открытой — с «, selected» (для кнопок-вкладок MAUI).
  - Команды `ShowImageModelsCommand`, `ShowAssistantModelCommand`.
  - Загрузка, «Refresh», «Save», «Cancel», поиск — без изменений.
- `AppResources`: `ImageModelsTab` = «Image models»; для второй вкладки — существующий `AssistantModelTitle` («Assistant model»); «selected» — существующий `AssistantSelected`.

Оболочки:
- MAUI: `Controls/SessionModelsView.xaml(.cs)` — `ContentView`, `x:DataType="pm:SessionModelsPageModel"`, корень — `Grid` со строкой `*` под содержимое вкладки и строкой `Auto` под кнопки. Полоса вкладок — две кнопки (существующие классы стилей, выбранная выделяется `DataTrigger` по `Is…TabSelected`, только ресурсы `Ig*`), `SemanticProperties.Description` = `…TabAutomationName`. Два блока содержимого, у каждого свой `ScrollView`, `IsVisible` по вкладке. В код-бихайнд переезжают `OnAssistantCheckedChanged` и порядок кнопок (`Grid.SetColumn`, macOS — «Cancel» слева).
- `SessionModelsPage.xaml` — только `controls:SessionModelsView`; `ChatPage.xaml` — тело панели моделей заменено на `controls:SessionModelsView` с `BindingContext="{Binding ModelsPane}"`; дубли обработчиков удалить.
- Avalonia: `SessionModelsView.axaml` — `TabControl` (`SelectedIndex` ↔ `SelectedTabIndex`, TwoWay), в каждом `TabItem` свой `ScrollViewer`; заголовки — `{markup:Res …}`, `AutomationProperties.Name`.

Шаги (TDD, ub-mode: сначала падающий тест, затем минимальный код, коммит после каждого зелёного шага):
1. Рефакторинг MAUI. Падающий текстовый тест `Presentation.Tests/Design/MauiSessionModelsMarkupTests` (как `MauiStylesTests`, разбор XAML как текста/XML): `Controls/SessionModelsView.xaml` существует; `SessionModelsPage.xaml` и `ChatPage.xaml` ссылаются на него и не содержат `{Binding AssistantModels}` (копия не вернётся). Затем вынос. Проверка: `dotnet build … -f net10.0-android`.
2. Состояние вкладок в модели страницы — тесты `SessionModelsPageModelTests`:
   - новая модель страницы → `SelectedTab = ImageModels`, `IsImageModelsTabSelected = true`, `SelectedTabIndex = 0`;
   - `ShowAssistantModelCommand` → вкладка «Assistant», флаги поменялись, `PropertyChanged` для флагов, индекса и имён доступности;
   - `SelectedTabIndex = 1` → `AssistantModel`; `SelectedTabIndex = 5` → вкладка не меняется;
   - отметить модель картинок → вкладка ассистента → выбрать ассистента → назад → галочка на месте; «Save» сохраняет и модели, и ассистента;
   - поиск при открытой вкладке ассистента фильтрует и `Models`, и `AssistantModels`;
   - `…TabAutomationName` содержит «selected» только у открытой вкладки.
3. Avalonia (headless, сначала падающие): в `SessionModelsView` есть `TabControl` с двумя `TabItem`, заголовки из `AppResources`, при открытии выбран первый; смена `TabItem` меняет `SelectedTab` и наоборот; в каждом `TabItem` свой `ScrollViewer`; `SaveButton` и `CancelButton` не внутри `TabControl`; `ViewsAccessibilityTests` — у вкладок есть имена; `ChatPaneTests` — в панели те же вкладки. Существующие `SessionModelsFlowTests` остаются зелёными.
4. Полоса вкладок в `Controls/SessionModelsView.xaml` (MAUI); сборка Android.
5. Документация: `docs/specs/chat-sessions.md` S5 — вкладки, вкладка по умолчанию, поиск фильтрует обе, кнопки общие; `docs/architecture.md` — у `ImageGen.Maui` добавить `Controls/` (`SessionsListView`, `StarterExamples`, `SessionModelsView`); `docs/knowledge/maui.md` — вкладки внутри страницы и область `GroupName`, если выяснится неочевидное.
6. Ручные пункты: в 016 (MAUI под Windows: вкладки на странице и в панели, Tab/Enter, диктор) и в 046 (Avalonia: стрелки по вкладкам, диктор).

Проверка: `dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj`, `dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj`, `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android`, `dotnet format ImageGen/<Проект>/<Проект>.csproj --verify-no-changes` для Presentation, Presentation.Tests, Avalonia, Avalonia.Tests, `scripts/check.sh`. Windows-цель MAUI — CI.

Файлы: `ImageGen/ImageGen.Presentation/PageModels/SessionModelsTab.cs` (новый), `ImageGen/ImageGen.Presentation/PageModels/SessionModelsPageModel.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.cs` (если строки объявляются там), `ImageGen/ImageGen.Maui/Controls/SessionModelsView.xaml(.cs)` (новые), `ImageGen/ImageGen.Maui/Pages/SessionModelsPage.xaml(.cs)`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml(.cs)`, `ImageGen/ImageGen.Avalonia/Views/SessionModelsView.axaml(.cs)`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionModelsPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/Design/MauiSessionModelsMarkupTests.cs` (новый), `ImageGen/ImageGen.Avalonia.Tests/Flows/SessionModelsFlowTests.cs`, `ImageGen/ImageGen.Avalonia.Tests/Views/ChatPaneTests.cs`, `ImageGen/ImageGen.Avalonia.Tests/Views/ViewsAccessibilityTests.cs`, `docs/specs/chat-sessions.md`, `docs/architecture.md`, `tasks/v0.1/016-*.md`, `tasks/backlog/046-*.md`.

## Отчёт

## Комментарии
