---
id: 012
status: planned
assignee:
claimed:
created: 2026-10-27
---

# MAUI: строки, платформенные сервисы и DI

## Описание
Подготовить `ImageGen.Maui` к страницам скелета:
- ссылка на `ImageGen.Data`;
- все строки интерфейса на английском в `.resx` с классом доступа;
- платформенные реализации `IAppPaths` и `ISecretStore` (SecureStorage);
- однократная инициализация при запуске (БД и пометка «Interrupted»);
- сервисы диалогов и навигации, маршруты;
- регистрация ядра и хранилища в `MauiProgram`.

Страниц здесь нет: шаблонная `MainPage` пока остаётся.

Зависит от: 034, 011.
Среда: только Windows с установленными нагрузками MAUI (сборка под Windows и Android).

## Критерии готовности
- Всё из плана есть в проекте с описанным поведением.
- Сборка под Windows и Android без ошибок и предупреждений.
- `dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes` проходит.

## Анализ
- Проверено пробной сборкой 2026-10-27 (`docs/knowledge/maui.md`, раздел «Проверено пробной сборкой»):
  - строго типизированный класс из `.resx` под Windows из командной строки не генерируется, поэтому `AppResources.cs` пишется вручную, а у `EmbeddedResource` задаётся `LogicalName`;
  - `SecureStorage` работает в неупакованном Windows-приложении;
  - `FileSystem.AppDataDirectory` на Windows — `%LOCALAPPDATA%\User Name\com.companyname.imagegen.maui\Data` (издатель из шаблона).
- Все строки скелета заводятся здесь одним файлом, чтобы задачи 013–015 не правили общий `.resx`.
- Базовый язык английский (решение владельца); русский добавляется позже файлом `AppResources.ru.resx`.
- `FireAndForgetSafeAsync` — единственное разрешённое место `async void` (`AGENTS.md`).
- Пробная реализация: `poc/v0.1/012-maui-infrastructure/` (подсказка, не копировать дословно, см. `poc/README.md`).

## План
1. `ImageGen.Maui.csproj`:
   - добавить `ProjectReference` на `../ImageGen.Data/ImageGen.Data.csproj` рядом со ссылкой на Core;
   - добавить `EmbeddedResource Update="Resources/Strings/AppResources.resx"` с `LogicalName="ImageGen.Maui.Resources.Strings.AppResources.resources"`.
2. `Resources/Strings/AppResources.resx` и `AppResources.cs`: статический класс `ImageGen.Maui.Resources.Strings.AppResources`, `ResourceManager` по имени из `LogicalName`, по свойству-строке на каждый ключ (`GetString(name, CultureInfo.CurrentUICulture)`). Ключи и тексты:

   | Ключ | Текст |
   |---|---|
   | AppTitle | ImageGen |
   | Ok / Cancel / ErrorTitle | OK / Cancel / Error |
   | ApiKeyTitle | OpenRouter key |
   | ApiKeyDescription | Enter your OpenRouter API key. It is kept in the system secure storage and sent only to OpenRouter. Prompts and images you send go to OpenRouter and the model providers. |
   | ApiKeyPlaceholder | sk-or-... |
   | ApiKeySave / ApiKeyChecking | Check and save / Checking the key... |
   | ApiKeyInvalid | OpenRouter rejected this key. |
   | ApiKeyNetworkError | Could not reach OpenRouter. Check the connection and try again. |
   | SessionsTitle / NewSession / DeleteSession | Sessions / New session / Delete |
   | DeleteSessionTitle / DeleteSessionMessage | Delete session / Delete this session with all its images? |
   | ChatPlaceholder / Send / AssistantThinking | Describe the image you want... / Send / Assistant is thinking... |
   | UserRole / AssistantRole | You / Assistant |
   | ModelsButtonFormat / RunPromptFormat | Models: {0} × {1} / Prompt: {0} |
   | StatusQueued / StatusRunning / StatusSucceeded | Queued / Generating... / Done |
   | StatusFailed / StatusCancelled / StatusFiltered | Error / Cancelled / Filtered by the provider |
   | SeedFormat / SeedNotSupported / CostFormat | seed {0} / seed not supported / ${0} |
   | Repeat / NewSeed / Edit | Repeat / New seed / Edit |
   | EditPromptTitle / EditPromptMessage / Generate | Edit prompt / The image is generated again with the same model and seed. / Generate |
   | ModelsTitle / ModelsSearchPlaceholder / RunsPerModelFormat | Session models / Search models / Runs per model: {0} |
   | ModelSupportsSeed / ModelNoSeed / Save / Refresh | seed / no seed / Save / Refresh |
3. `Services/`:
   - `MauiAppPaths : IAppPaths` — `DataDirectory` = `FileSystem.Current.AppDataDirectory`.
   - `MauiSecretStore : ISecretStore` — get/set/remove через `SecureStorage.Default`.
   - `AppInitializer(SqliteDatabase, GenerationService)` — `EnsureInitializedAsync()`: первый вызов запускает `database.InitializeAsync` и затем `generationService.RecoverInterruptedAsync`, все последующие (в том числе параллельные) получают ту же задачу (ленивое поле под `lock`).
   - `IDialogService` с `AlertAsync(title, message)`, `ConfirmAsync(title, message, accept, cancel) → bool`, `PromptAsync(title, message, accept, cancel, initialValue) → string?` (null при отмене); `ShellDialogService` — через `Shell.Current.DisplayAlertAsync` / `DisplayPromptAsync` (кнопка OK — `AppResources.Ok`, `maxLength` промпта 4000).
   - `INavigationService` с `GoToAsync(route)` и `GoBackAsync()`; `ShellNavigationService` — через `Shell.Current.GoToAsync` (назад — `".."`).
4. `Routes.cs`: константы `ApiKey = "apikey"`, `Chat = "chat"`, `SessionModels = "models"`, параметр `SessionIdParameter = "sessionId"`; `ChatFor(sessionId)` и `SessionModelsFor(sessionId)` дают `маршрут?sessionId=<guid>`.
5. `Utilities/TaskExtensions.FireAndForgetSafeAsync(this Task)`: `async void`, ждёт задачу и пишет исключение в `Debug`, наружу не бросает.
6. `MauiProgram.cs`: после `UseMauiApp`/`UseMauiCommunityToolkit` и шрифтов шаблона, `AddDebug` в DEBUG, регистрации singleton: `IAppPaths` → `MauiAppPaths`, `ISecretStore` → `MauiSecretStore`, `AddImageGenCore()`, `AddImageGenData()`, `AppInitializer`, `IDialogService`, `INavigationService`. В конце регистраций — строка-комментарий `// Pages and page models`, после которой задачи 013–015 добавляют страницы.

Файлы: `ImageGen/ImageGen.Maui/ImageGen.Maui.csproj`, `ImageGen/ImageGen.Maui/MauiProgram.cs`, `ImageGen/ImageGen.Maui/Routes.cs`, `ImageGen/ImageGen.Maui/Resources/Strings/*`, `ImageGen/ImageGen.Maui/Services/*.cs`, `ImageGen/ImageGen.Maui/Utilities/TaskExtensions.cs`

### Проверка
```
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
scripts/check.sh
```

## Отчёт

## Комментарии
