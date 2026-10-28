---
id: 013
status: done
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

Статус `in-progress`: критерии «запуск под Windows» и «сборка под Windows» не проверены (облачная сессия, XAML-компилятор только под Windows); ручная проверка за задачей 016. Занятость не снималась.

- Сделано (план пп. 1–5): удалены `MainPage.xaml` и `MainPage.xaml.cs`; `AppShell.xaml` с одним `ShellContent` на `SessionsPage` (`DataTemplate`, заголовок из `AppResources`); `Pages/ApiKeyPage.xaml(.cs)` и `Pages/SessionsPage.xaml(.cs)`; `PageModels/ApiKeyPageModel.cs`, `PageModels/SessionItemViewModel.cs`, `PageModels/SessionsPageModel.cs`; регистрации в `MauiProgram.cs` после `// Pages and page models`.
- Открытый вопрос из 012 (вызов `AppInitializer` при старте): решено по плану 013. Вызов стоит в `SessionsPageModel.OnAppearingAsync` (первая страница), `App` не менялся. Описание 012 говорит «при запуске»: вызов выполняется при первом и каждом последующем появлении списка сессий, но сама инициализация однократна (`AppInitializer`). Это явное отклонение от формулировки 012, согласованное с планом 013.
- Логика вынесена в PageModel'ы без типов MAUI и покрыта тестами, написанными до реализации:
  - `ImageGen.Core.Tests/MauiLinked/ApiKeyPageModelTests.cs` (8): доступность сохранения, принятый/отклонённый/сетевой ключ, состояние «проверка», неожиданная ошибка;
  - `ImageGen.Core.Tests/MauiLinked/SessionItemViewModelTests.cs` (4): поля строки, формат времени, команды открытия и удаления;
  - `ImageGen.Data.Tests/MauiLinked/SessionsPageModelTests.cs` (9): переход на ключ без ключа, порядок «новые сверху», замена строк без дублей, ошибка списка → alert, создание сессии → чат, открытие, удаление с подтверждением и отменой.
- Результаты: `dotnet test` ImageGen.Core.Tests 114 passed, 0 failed (было 102); ImageGen.Data.Tests 38 passed, 0 failed (было 29). `dotnet format --verify-no-changes`: ImageGen.Maui, ImageGen.Core, ImageGen.Core.Tests, ImageGen.Data, ImageGen.Data.Tests: все код выхода 0. `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android`: 0 Error(s), 0 Warning(s). `scripts/check.sh`: все проверки пройдены.
- Тестовые проекты: в `ImageGen.Core.Tests` и `ImageGen.Data.Tests` подключены через `Compile`/`EmbeddedResource` с `Link` файлы PageModel'ов, `IDialogService`, `INavigationService`; в `ImageGen.Data.Tests` дополнительно `Routes.cs` и `AppResources` (нужны `SessionsPageModel`). Добавлена ссылка `CommunityToolkit.Mvvm` с версией из `Directory.Packages.props`.
- Отклонения от плана (мелкие):
  - при ошибке списка и создания сессии текст alert — `ex.Message` (ключа под общий текст ошибки в таблице 012 нет); неожиданная ошибка сохранения ключа тоже уходит в alert `ErrorTitle`, план её не описывает;
  - индикатор загрузки списка без текста и с `AutomationProperties.IsInAccessibleTree="False"`: подходящего ключа строки нет (`AssistantThinking` не подходит);
  - строка списка: `TapGestureRecognizer` на блоке заголовка и даты, кнопка Delete справа; `SemanticProperties.Description` блока — заголовок сессии;
  - сортировка «новые сверху» сделана в `SessionsPageModel` (S1.1), а не полагается на порядок репозитория.
- Не проверено (нужен Windows, задача 016): сборка `-f net10.0-windows10.0.19041.0`; запуск и первый экран (страница ключа при первом запуске, пустой список после сохранения верного ключа); `DisplayAlertAsync`/`DisplayPromptAsync` в Shell; `SemanticScreenReader.Announce` и реальная озвучка; разрешение `SessionsPage` через DI в `DataTemplate` Shell; переход `apikey` через `AddTransientWithShellRoute`; визуальная разметка (`MaximumWidthRequest=600`, касание 44). XAML на Android проверен компилятором SourceGen (сборка без ошибок); XAML-разметка страниц в тестах не покрыта.
- Переход `chat?sessionId=...` работает только после задачи 014 (ожидаемо по плану).
- `docs/architecture.md` не менялся: раскладка и правила слоёв совпадают с реализацией.

**Проверено на Windows (2026-10-28, ревью после мерджа PR #14; Windows 11, SDK 10.0.402):**
- `dotnet format --verify-no-changes` для ImageGen.Maui, Core, Core.Tests, Data, Data.Tests: без изменений.
- `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0` и `-f net10.0-android`: 0 ошибок, 0 предупреждений.
- `dotnet test`: ImageGen.Core.Tests 193 из 193 пять прогонов подряд (после c9d5ea9 гонки нет), ImageGen.Data.Tests 38 из 38.
- Запуск `ImageGen.Maui.exe` (Debug): приложение стартует и работает без падения (контейнер DI и Shell собираются). Интерфейс и сценарий с ключом — задача 016.
- Закрыто: строка «сборка под Windows» из критериев выполнена; ручные пункты критериев отнесены к 016.

## Комментарии
