---
id: 014
status: in-progress
assignee: claude-qnrqyt
claimed: 2026-10-27T00:00:00Z
created: 2026-10-27
---

# MAUI: чат сессии и карточки генераций

## Описание
Страница чата: лента из сообщений и прогонов в порядке `TimelineOrdering`, поле ввода и кнопка Send (сообщение уходит ассистенту), кнопка моделей сессии с подписью «Models: N × M». Прогон показывается промптом и карточками генераций: картинка, статус, модель, сид или «seed not supported», цена, текст ошибки, кнопки Repeat, New seed, Edit. Карточки обновляются по событию `GenerationUpdated` в UI-потоке.

Зависит от: 013.
Среда: только Windows с установленными нагрузками MAUI (сборка под Windows и Android).

## Критерии готовности
- Страница и модели из плана есть и работают как описано.
- Сборка под Windows и Android без ошибок и предупреждений; `dotnet format` для MAUI-проекта проходит.
- Ручная проверка (задача 016): сообщение «draw a red fox» даёт прогон с карточкой, карточка переходит из «Queued/Generating...» в «Done» с картинкой без перезагрузки страницы.

## Анализ
- Спецификация: S2, S3, S4 (`docs/specs/chat-sessions.md`).
- Строки ленты разных типов выбирает `ChatItemTemplateSelector`. Команды карточки лежат в `GenerationItemViewModel` и вызывают методы `ChatPageModel` (компилируемые привязки без ссылок на родителя).
- После своих действий (Send, Repeat, New seed, Edit) страница перечитывает всю ленту; событие обновляет только одну карточку. Подписка на событие снимается при уходе со страницы.
- Edit использует `DisplayPromptAsync` с точным промптом генерации; пустой ввод или Cancel ничего не запускают.
- Маршрут `models` регистрирует задача 015; до неё кнопка моделей упадёт с ошибкой навигации (ожидаемо).
- Пробная реализация: `poc/v0.1/014-maui-chat/` (подсказка, не копировать дословно, см. `poc/README.md`).

## План
1. Модели строк (`PageModels/`):
   - `MessageItemViewModel(ChatMessage)`: `RoleText` (`UserRole`/`AssistantRole`), `Text`, `IsUser`, `IsError`.
   - `RunItemViewModel(Run, карточки)`: `Id`, `PromptText` (`RunPromptFormat`), `Generations` — коллекция карточек.
   - `GenerationItemViewModel(Generation, IImageFileStore, repeat, newSeed, edit)`: `Id`, `Prompt` (точный промпт генерации), команды `RepeatCommand`, `NewSeedCommand`, `EditCommand` (доступны, только когда генерация завершена), метод `Update(Generation)` — вызывается в UI-потоке и пересчитывает:
     - `StatusText` по статусу: Queued → `StatusQueued`, Running → `StatusRunning`, Succeeded → `StatusSucceeded`, Failed → `StatusFailed`, Cancelled → `StatusCancelled`, Filtered → `StatusFiltered`;
     - `DetailsText` = `<модель> · <seed N | seed not supported>[ · $<цена до 4 знаков>]`;
     - `ErrorText` = текст ошибки или пусто;
     - `HasImage`/`Image` — только для Succeeded с путём (`ImageSource.FromFile(files.GetFullPath(path))`);
     - `IsRunning` для Queued и Running, `IsFinished` = `Generation.IsFinished`; обновить доступность команд.
   - `ChatItemTemplateSelector : DataTemplateSelector`: свойства `MessageTemplate` и `RunTemplate`; `RunItemViewModel` → `RunTemplate`, иначе `MessageTemplate`; шаблон не задан → `InvalidOperationException`.
2. `PageModels/ChatPageModel` (`IQueryAttributable`, параметр `sessionId` из `Routes`):
   - Зависимости: `SessionService`, `AssistantService`, `GenerationService`, `IImageFileStore`, `INavigationService`, `IDialogService`.
   - Состояние: `Items` (сообщения и прогоны), словарь «id генерации → карточка», `Title`, `ModelsButtonText`, `InputText`, `IsBusy`.
   - Поток:
     ```
     OnAppearingAsync: отписаться и подписаться на GenerationUpdated (без двойной подписки), Reload
     OnDisappearing:   отписаться
     Reload: timeline = GetTimelineAsync(sessionId)
             Title, ModelsButtonText = ModelsButtonFormat(число моделей, RunsPerModel)
             очистить Items и словарь
             для каждой записи TimelineOrdering.Order(timeline):
                 сообщение → MessageItemViewModel
                 прогон → карточки его генераций в словарь, RunItemViewModel
             ошибка → AlertAsync
     GenerationUpdated(e): чужая сессия → игнорировать
                           иначе MainThread.BeginInvokeOnMainThread → карточка.Update(e.Generation)
     Send (доступна, если не занято и текст не пустой):
             запомнить текст, очистить поле, IsBusy
             AssistantService.SendUserMessageAsync
             ошибка → вернуть текст в поле, AlertAsync
             IsBusy = false, Reload
     OpenModels → GoToAsync(Routes.SessionModelsFor(sessionId))
     Repeat / New seed → GenerationService.RepeatAsync / NewSeedAsync, ошибка → AlertAsync, Reload
     Edit → PromptAsync(EditPromptTitle, EditPromptMessage, Generate, Cancel, card.Prompt)
             пусто или отмена → ничего; иначе GenerationService.EditAsync, Reload
     ```
3. `Pages/ChatPage` (конструктор получает модель; `OnAppearing` → `OnAppearingAsync().FireAndForgetSafeAsync()`, `OnDisappearing` → `OnDisappearing()`; заголовок привязан к `Title`):
   - Сверху кнопка с `ModelsButtonText`.
   - `CollectionView` ленты с `ChatItemTemplateSelector`:
     - сообщение: роль мелким жирным, текст с переносом, ошибка красным (`DataTrigger` по `IsError`);
     - прогон: промпт курсивом, ниже карточки в `FlexLayout` с переносом (ширина карточки 280, высота картинки 256): картинка или индикатор, статус, детали, ошибка, кнопки Repeat / New seed / Edit.
   - Строка «Assistant is thinking...» видна при `IsBusy`.
   - Внизу `Editor` (placeholder `ChatPlaceholder`) и кнопка Send.
   - Все шаблоны с `x:DataType`; `SemanticProperties` у сообщений, карточек, поля и кнопок.
4. `MauiProgram.cs`: после регистрации `ApiKeyPage` — `AddTransientWithShellRoute<ChatPage, ChatPageModel>(Routes.Chat)`.

Файлы: `ImageGen/ImageGen.Maui/MauiProgram.cs`, `ImageGen/ImageGen.Maui/PageModels/MessageItemViewModel.cs`, `ImageGen/ImageGen.Maui/PageModels/GenerationItemViewModel.cs`, `ImageGen/ImageGen.Maui/PageModels/RunItemViewModel.cs`, `ImageGen/ImageGen.Maui/PageModels/ChatItemTemplateSelector.cs`, `ImageGen/ImageGen.Maui/PageModels/ChatPageModel.cs`, `ImageGen/ImageGen.Maui/Pages/ChatPage.*`

### Проверка
```
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
scripts/check.sh
```

## Отчёт

Статус `in-progress`: критерий «сборка под Windows» не проверен (облачная сессия, XAML-компилятор только под Windows); ручная проверка «draw a red fox» за задачей 016. Занятость не снималась. Статусы 012 и 013 не менялись.

- Сделано (план пп. 1–4):
  - модели строк: `PageModels/MessageItemViewModel.cs`, `PageModels/RunItemViewModel.cs`, `PageModels/GenerationItemViewModel.cs` (статус, `DetailsText`, `ErrorText`, `HasImage`/`ImageFilePath`, `IsRunning`, `IsFinished`, команды Repeat/NewSeed/Edit с `CanExecute` по `IsFinished`, метод `Update`);
  - выбор шаблона: `PageModels/ChatItemTemplateChoice.cs` (логика без типов MAUI, исключение при пустом шаблоне) и `PageModels/ChatItemTemplateSelector.cs` (`DataTemplateSelector`);
  - `PageModels/ChatPageModel.cs`: поток по плану (`OnAppearingAsync` с однократной подпиской, `OnDisappearing`, `ReloadAsync`, `SendCommand`, `OpenModelsCommand`, Repeat/NewSeed/Edit через общий `RunAndReloadAsync`); карточка обновляется по `GenerationUpdated` в UI-потоке;
  - `Pages/ChatPage.xaml(.cs)`: `x:DataType` у страницы и всех шаблонов, `SemanticProperties` у сообщений, карточек, поля и кнопок, тексты из `AppResources`, ошибка красным через `DataTrigger`, индикатор «Assistant is thinking...» при `IsBusy`;
  - `Converters/FilePathToImageSourceConverter.cs`, `Services/IMainThread.cs`, `Services/MauiMainThread.cs`;
  - `MauiProgram.cs`: `AddSingleton<IMainThread, MauiMainThread>()` и `AddTransientWithShellRoute<ChatPage, ChatPageModel>(Routes.Chat)` после `ApiKeyPage`;
  - `Resources/Styles/Colors.xaml`: два цвета ошибки (светлая и тёмная тема);
  - новых строк нет: все нужные ключи уже были в `AppResources.resx` и `AppResources.cs`.
- Тесты (TDD: сначала падающие, затем реализация), все в `ImageGen.Core.Tests/MauiLinked/` через `Compile`/`Link`, рантайм MAUI не нужен: `GenerationItemViewModelTests` (19), `MessageItemViewModelTests` (5), `RunItemViewModelTests` (4), `ChatItemTemplateChoiceTests` (5), `ChatPageModelTests` (23). В `ChatPageModelTests` реальные `SessionService`, `AssistantService` и `GenerationService` с подменёнными репозиториями и клиентом OpenRouter; фоновые генерации дожидаются `WhenIdleAsync`.
- Результаты:
  - `dotnet test` ImageGen.Core.Tests: 170 passed, 0 failed (было 114); три прогона подряд без отличий;
  - `dotnet test` ImageGen.Data.Tests: 38 passed, 0 failed (без изменений);
  - `dotnet format --verify-no-changes`: ImageGen.Maui, ImageGen.Core, ImageGen.Core.Tests, ImageGen.Data, ImageGen.Data.Tests, код выхода 0 (для ImageGen.Core.Tests применён `dotnet format` к отступам новых файлов);
  - `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android`: 0 Error(s), 0 Warning(s);
  - контрольная проверка XAML: неверная привязка в `ChatPage.xaml` даёт ошибку сборки MAUIG2045, то есть привязки проверяются генератором XAML уже в Android-сборке; тестовая правка откачена.
- Отклонения от плана:
  - `ChatPageModel` не реализует `IQueryAttributable` (тип MAUI не попадает в тесты на Linux). `ChatPage` реализует его и передаёт query в `ChatPageModel.ApplyQueryAttributes(IDictionary<string, object>)`;
  - `GenerationItemViewModel` отдаёт путь к файлу (`ImageFilePath`), а не `ImageSource`; преобразование делает `FilePathToImageSourceConverter` в XAML;
  - добавлен `IMainThread` (обёртка над `MainThread.BeginInvokeOnMainThread`), чтобы тестировать обновление карточки;
  - «отфильтровано» показывает причину (`HasErrorText`), но не выделяется как ошибка (`HasError` только для статуса «ошибка»);
  - цена: `$` и до 4 знаков после запятой (`0.####`); если `CostUsd` неизвестна, части цены нет;
  - `ReloadAsync` вызывается и после отмены Edit (данные не меняются);
  - `docs/architecture.md`: в перечень `ImageGen.Maui` добавлен `Converters/` (единственная правка docs, раскладка не менялась по существу);
  - маршрут `models` регистрирует задача 015: до неё кнопка моделей показывает alert об ошибке навигации (ожидаемо).
- Не проверено (нужен Windows или рантайм MAUI, задача 016):
  - сборка `-f net10.0-windows10.0.19041.0` (XAML-компилятор только под Windows);
  - запуск на устройстве: отрисовка ленты, `CollectionView` с `DataTemplateSelector`, `BindableLayout` в `FlexLayout`;
  - `ImageSource.FromFile` с абсолютным путём на устройстве и обновление картинки без перезагрузки страницы;
  - `DisplayPromptAsync` и `DisplayAlertAsync` в Shell, переход `chat?sessionId=...` через query;
  - визуал: ширина карточки 280, высота картинки 256, касание 44, тёмная тема (`AppThemeBinding` в `Setter` внутри `DataTrigger`);
  - `SemanticProperties` на рантайме (голосовой вывод);
  - Android: только сборка, без запуска (эмулятор в облачной сессии недоступен, см. `docs/knowledge/claude-cloud-session.md`).

## Комментарии
