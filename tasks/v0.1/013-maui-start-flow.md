---
id: 013
status: planned
assignee:
claimed:
created: 2026-10-27
---

# MAUI: запуск, ввод ключа и список сессий

## Описание
Первый экран приложения — список сессий. При каждом показе он один раз инициализирует приложение (`AppInitializer`), и если ключа OpenRouter нет, открывает страницу ввода ключа. Страница ключа проверяет его через OpenRouter и сохраняет только принятый ключ. Список сессий: создать новую (сразу открывает чат), открыть, удалить с подтверждением. Шаблонная `MainPage` удаляется.

Зависит от: 012.
Среда: только Windows с установленными нагрузками MAUI (сборка под Windows и Android).

## Критерии готовности
- Страницы и модели из плана есть, работают как описано; `MainPage.xaml` и `MainPage.xaml.cs` удалены.
- Сборка под Windows и Android без ошибок и предупреждений; `dotnet format` для MAUI-проекта проходит.
- Ручная проверка на Windows (делает человек, задача 016): при первом запуске открывается страница ключа; после сохранения верного ключа — пустой список сессий.

## Анализ
- Спецификация: S1.1–S1.2 (`docs/specs/chat-sessions.md`), F1.1–F1.3 и F10.6 (предупреждение, куда уходят промпты, — в тексте `ApiKeyDescription`).
- Команды строк списка лежат в `SessionItemViewModel` и вызывают методы владельца: так привязки в шаблоне остаются компилируемыми (`x:DataType`) без ссылок на родительскую страницу.
- Маршрут `chat` регистрирует задача 014; до неё кнопка «New session» создаёт сессию, но переход упадёт с ошибкой навигации (ожидаемо).
- Пробная реализация: `poc/v0.1/013-maui-start-flow/` (подсказка, не копировать дословно, см. `poc/README.md`). Проверено сборкой под Windows и Android и запуском под Windows 2026-10-27: приложение стартует и создаёт `imagegen.db`.

## План
1. Удалить `MainPage.xaml` и `MainPage.xaml.cs`.
2. `AppShell.xaml`: один `ShellContent` с `SessionsPage` (через `DataTemplate`, страница из DI), заголовок `AppResources.SessionsTitle`.
3. Страница ключа (`PageModels/ApiKeyPageModel`, `Pages/ApiKeyPage`):
   - Свойства: `ApiKey`, `IsBusy`, `StatusText`; команда `SaveCommand` доступна, когда не занято и ключ не пустой.
   - Машина состояний:
     ```
     Ввод --Save--> Проверка (IsBusy, StatusText = ApiKeyChecking)
     Проверка --Valid--> очистить поле и статус, GoBackAsync (к списку сессий)
     Проверка --Invalid--> Ввод, StatusText = ApiKeyInvalid
     Проверка --NetworkError--> Ввод, StatusText = ApiKeyNetworkError
     ```
     Статус объявляется экранному диктору (`SemanticScreenReader.Announce`).
   - Разметка (ширина до 600): описание `ApiKeyDescription`, поле `Entry` с `IsPassword`, placeholder `ApiKeyPlaceholder`, кнопка `ApiKeySave`, индикатор занятости, строка статуса. У поля и кнопки — `SemanticProperties`.
4. Список сессий (`PageModels/SessionItemViewModel`, `PageModels/SessionsPageModel`, `Pages/SessionsPage`):
   - `SessionItemViewModel(session, open, delete)`: `Id`, `Title`, `UpdatedText` (локальное время, формат `g`), команды `OpenCommand` и `DeleteCommand` вызывают переданные функции.
   - `SessionsPageModel.OnAppearingAsync()` (страница вызывает его в `OnAppearing` через `FireAndForgetSafeAsync`):
     ```
     IsBusy
     await initializer.EnsureInitializedAsync()
     нет ключа → GoToAsync(Routes.ApiKey) и выйти
     перечитать список сессий
     ошибка → AlertAsync(ErrorTitle, текст)
     ```
   - `NewSessionCommand`: создать сессию → `GoToAsync(Routes.ChatFor(id))`.
   - Открыть → `GoToAsync(Routes.ChatFor(id))`. Удалить → `ConfirmAsync(DeleteSessionTitle, DeleteSessionMessage, DeleteSession, Cancel)`, при согласии `SessionService.DeleteAsync` и убрать строку из списка.
   - Разметка: кнопка «New session» сверху, индикатор, `CollectionView` без выбора: строка — заголовок жирным, дата мелко (нажатие открывает), кнопка Delete справа. Компилируемые привязки (`x:DataType`), `SemanticProperties` у строк и кнопок.
5. `MauiProgram.cs` после `// Pages and page models`: `SessionsPage` и `SessionsPageModel` — singleton; `ApiKeyPage` и `ApiKeyPageModel` — `AddTransientWithShellRoute` с маршрутом `Routes.ApiKey`.

Файлы: `ImageGen/ImageGen.Maui/MainPage.xaml` (удаление), `ImageGen/ImageGen.Maui/MainPage.xaml.cs` (удаление), `ImageGen/ImageGen.Maui/AppShell.xaml`, `ImageGen/ImageGen.Maui/MauiProgram.cs`, `ImageGen/ImageGen.Maui/PageModels/ApiKeyPageModel.cs`, `ImageGen/ImageGen.Maui/PageModels/SessionItemViewModel.cs`, `ImageGen/ImageGen.Maui/PageModels/SessionsPageModel.cs`, `ImageGen/ImageGen.Maui/Pages/ApiKeyPage.*`, `ImageGen/ImageGen.Maui/Pages/SessionsPage.*`

### Проверка
```
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
scripts/check.sh
```

## Отчёт

## Комментарии
