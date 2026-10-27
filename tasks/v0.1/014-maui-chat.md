---
id: 014
status: planned
assignee:
claimed:
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

## Комментарии
