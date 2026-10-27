---
id: 012
status: planned
assignee:
claimed:
created: 2026-10-27
---

# MAUI: строки, платформенные сервисы и DI

## Описание
Подготовить `ImageGen.Maui` к страницам скелета: ссылка на `ImageGen.Data`, все строки интерфейса на английском в `.resx` с классом доступа, платформенные реализации `IAppPaths` и `ISecretStore` (SecureStorage), однократная инициализация при запуске (БД и пометка «Interrupted»), сервисы диалогов и навигации, маршруты, регистрация ядра и хранилища в `MauiProgram`. Страниц здесь нет: шаблонная `MainPage` пока остаётся.

Зависит от: 001, 011.

## Критерии готовности
- Файлы из плана созданы с содержимым как в плане; `ImageGen.Maui.csproj` и `MauiProgram.cs` изменены как в плане.
- Сборка под Windows и Android без ошибок и предупреждений.
- `dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes` проходит.

## Анализ
- Проверено пробной сборкой 2026-10-27 (`docs/knowledge/maui.md`, раздел «Проверено пробной сборкой»):
  - строго типизированный класс из `.resx` под Windows из командной строки не генерируется, поэтому `AppResources.cs` написан вручную, а у `EmbeddedResource` задан `LogicalName`;
  - `SecureStorage` работает в неупакованном Windows-приложении;
  - `FileSystem.AppDataDirectory` на Windows — `%LOCALAPPDATA%\User Name\com.companyname.imagegen.maui\Data` (издатель из шаблона).
- Все строки скелета заводятся здесь одним файлом, чтобы задачи 013–015 не правили общий `.resx`.
- Базовый язык английский (решение владельца); русский добавляется позже файлом `AppResources.ru.resx`.
- `FireAndForgetSafeAsync` — единственное разрешённое место `async void` (`AGENTS.md`).

## План
1. В `ImageGen/ImageGen.Maui/ImageGen.Maui.csproj`:
   - после строки `<ProjectReference Include="../ImageGen.Core/ImageGen.Core.csproj" />` добавить строку `<ProjectReference Include="../ImageGen.Data/ImageGen.Data.csproj" />`;
   - перед закрывающим `</Project>` добавить:
     ```xml
     <ItemGroup>
       <EmbeddedResource Update="Resources/Strings/AppResources.resx" LogicalName="ImageGen.Maui.Resources.Strings.AppResources.resources" />
     </ItemGroup>
     ```

2. Создать файлы (содержимое дословно):

   `ImageGen/ImageGen.Maui/Resources/Strings/AppResources.resx`
   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <root>
     <resheader name="resmimetype">
       <value>text/microsoft-resx</value>
     </resheader>
     <resheader name="version">
       <value>2.0</value>
     </resheader>
     <resheader name="reader">
       <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
     </resheader>
     <resheader name="writer">
       <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
     </resheader>
     <data name="AppTitle" xml:space="preserve"><value>ImageGen</value></data>
     <data name="Ok" xml:space="preserve"><value>OK</value></data>
     <data name="Cancel" xml:space="preserve"><value>Cancel</value></data>
     <data name="ErrorTitle" xml:space="preserve"><value>Error</value></data>
     <data name="ApiKeyTitle" xml:space="preserve"><value>OpenRouter key</value></data>
     <data name="ApiKeyDescription" xml:space="preserve"><value>Enter your OpenRouter API key. It is kept in the system secure storage and sent only to OpenRouter. Prompts and images you send go to OpenRouter and the model providers.</value></data>
     <data name="ApiKeyPlaceholder" xml:space="preserve"><value>sk-or-...</value></data>
     <data name="ApiKeySave" xml:space="preserve"><value>Check and save</value></data>
     <data name="ApiKeyChecking" xml:space="preserve"><value>Checking the key...</value></data>
     <data name="ApiKeyInvalid" xml:space="preserve"><value>OpenRouter rejected this key.</value></data>
     <data name="ApiKeyNetworkError" xml:space="preserve"><value>Could not reach OpenRouter. Check the connection and try again.</value></data>
     <data name="SessionsTitle" xml:space="preserve"><value>Sessions</value></data>
     <data name="NewSession" xml:space="preserve"><value>New session</value></data>
     <data name="DeleteSession" xml:space="preserve"><value>Delete</value></data>
     <data name="DeleteSessionTitle" xml:space="preserve"><value>Delete session</value></data>
     <data name="DeleteSessionMessage" xml:space="preserve"><value>Delete this session with all its images?</value></data>
     <data name="ChatPlaceholder" xml:space="preserve"><value>Describe the image you want...</value></data>
     <data name="Send" xml:space="preserve"><value>Send</value></data>
     <data name="AssistantThinking" xml:space="preserve"><value>Assistant is thinking...</value></data>
     <data name="UserRole" xml:space="preserve"><value>You</value></data>
     <data name="AssistantRole" xml:space="preserve"><value>Assistant</value></data>
     <data name="ModelsButtonFormat" xml:space="preserve"><value>Models: {0} × {1}</value></data>
     <data name="RunPromptFormat" xml:space="preserve"><value>Prompt: {0}</value></data>
     <data name="StatusQueued" xml:space="preserve"><value>Queued</value></data>
     <data name="StatusRunning" xml:space="preserve"><value>Generating...</value></data>
     <data name="StatusSucceeded" xml:space="preserve"><value>Done</value></data>
     <data name="StatusFailed" xml:space="preserve"><value>Error</value></data>
     <data name="StatusCancelled" xml:space="preserve"><value>Cancelled</value></data>
     <data name="StatusFiltered" xml:space="preserve"><value>Filtered by the provider</value></data>
     <data name="SeedFormat" xml:space="preserve"><value>seed {0}</value></data>
     <data name="SeedNotSupported" xml:space="preserve"><value>seed not supported</value></data>
     <data name="CostFormat" xml:space="preserve"><value>${0}</value></data>
     <data name="Repeat" xml:space="preserve"><value>Repeat</value></data>
     <data name="NewSeed" xml:space="preserve"><value>New seed</value></data>
     <data name="Edit" xml:space="preserve"><value>Edit</value></data>
     <data name="EditPromptTitle" xml:space="preserve"><value>Edit prompt</value></data>
     <data name="EditPromptMessage" xml:space="preserve"><value>The image is generated again with the same model and seed.</value></data>
     <data name="Generate" xml:space="preserve"><value>Generate</value></data>
     <data name="ModelsTitle" xml:space="preserve"><value>Session models</value></data>
     <data name="ModelsSearchPlaceholder" xml:space="preserve"><value>Search models</value></data>
     <data name="RunsPerModelFormat" xml:space="preserve"><value>Runs per model: {0}</value></data>
     <data name="ModelSupportsSeed" xml:space="preserve"><value>seed</value></data>
     <data name="ModelNoSeed" xml:space="preserve"><value>no seed</value></data>
     <data name="Save" xml:space="preserve"><value>Save</value></data>
     <data name="Refresh" xml:space="preserve"><value>Refresh</value></data>
   </root>
   ```

   `ImageGen/ImageGen.Maui/Resources/Strings/AppResources.cs`
   ```csharp
   using System.Globalization;
   using System.Resources;

   namespace ImageGen.Maui.Resources.Strings;

   /// <summary>
   /// Access to <c>AppResources.resx</c>. Written by hand: Visual Studio code generation does not run for the
   /// Windows target in command-line builds. Add a property here for every new string in the .resx file.
   /// </summary>
   public static class AppResources
   {
       private static readonly ResourceManager ResourceManager =
           new("ImageGen.Maui.Resources.Strings.AppResources", typeof(AppResources).Assembly);

       public static string AppTitle => Get(nameof(AppTitle));

       public static string Ok => Get(nameof(Ok));

       public static string Cancel => Get(nameof(Cancel));

       public static string ErrorTitle => Get(nameof(ErrorTitle));

       public static string ApiKeyTitle => Get(nameof(ApiKeyTitle));

       public static string ApiKeyDescription => Get(nameof(ApiKeyDescription));

       public static string ApiKeyPlaceholder => Get(nameof(ApiKeyPlaceholder));

       public static string ApiKeySave => Get(nameof(ApiKeySave));

       public static string ApiKeyChecking => Get(nameof(ApiKeyChecking));

       public static string ApiKeyInvalid => Get(nameof(ApiKeyInvalid));

       public static string ApiKeyNetworkError => Get(nameof(ApiKeyNetworkError));

       public static string SessionsTitle => Get(nameof(SessionsTitle));

       public static string NewSession => Get(nameof(NewSession));

       public static string DeleteSession => Get(nameof(DeleteSession));

       public static string DeleteSessionTitle => Get(nameof(DeleteSessionTitle));

       public static string DeleteSessionMessage => Get(nameof(DeleteSessionMessage));

       public static string ChatPlaceholder => Get(nameof(ChatPlaceholder));

       public static string Send => Get(nameof(Send));

       public static string AssistantThinking => Get(nameof(AssistantThinking));

       public static string UserRole => Get(nameof(UserRole));

       public static string AssistantRole => Get(nameof(AssistantRole));

       public static string ModelsButtonFormat => Get(nameof(ModelsButtonFormat));

       public static string RunPromptFormat => Get(nameof(RunPromptFormat));

       public static string StatusQueued => Get(nameof(StatusQueued));

       public static string StatusRunning => Get(nameof(StatusRunning));

       public static string StatusSucceeded => Get(nameof(StatusSucceeded));

       public static string StatusFailed => Get(nameof(StatusFailed));

       public static string StatusCancelled => Get(nameof(StatusCancelled));

       public static string StatusFiltered => Get(nameof(StatusFiltered));

       public static string SeedFormat => Get(nameof(SeedFormat));

       public static string SeedNotSupported => Get(nameof(SeedNotSupported));

       public static string CostFormat => Get(nameof(CostFormat));

       public static string Repeat => Get(nameof(Repeat));

       public static string NewSeed => Get(nameof(NewSeed));

       public static string Edit => Get(nameof(Edit));

       public static string EditPromptTitle => Get(nameof(EditPromptTitle));

       public static string EditPromptMessage => Get(nameof(EditPromptMessage));

       public static string Generate => Get(nameof(Generate));

       public static string ModelsTitle => Get(nameof(ModelsTitle));

       public static string ModelsSearchPlaceholder => Get(nameof(ModelsSearchPlaceholder));

       public static string RunsPerModelFormat => Get(nameof(RunsPerModelFormat));

       public static string ModelSupportsSeed => Get(nameof(ModelSupportsSeed));

       public static string ModelNoSeed => Get(nameof(ModelNoSeed));

       public static string Save => Get(nameof(Save));

       public static string Refresh => Get(nameof(Refresh));

       private static string Get(string name) =>
           ResourceManager.GetString(name, CultureInfo.CurrentUICulture) ?? name;
   }
   ```

   `ImageGen/ImageGen.Maui/Services/MauiAppPaths.cs`
   ```csharp
   using ImageGen.Core.Platform;

   namespace ImageGen.Maui.Services;

   public sealed class MauiAppPaths : IAppPaths
   {
       public string DataDirectory => FileSystem.Current.AppDataDirectory;
   }
   ```

   `ImageGen/ImageGen.Maui/Services/MauiSecretStore.cs`
   ```csharp
   using ImageGen.Core.Platform;

   namespace ImageGen.Maui.Services;

   /// <summary>Secrets in MAUI SecureStorage (Keychain, Keystore, Windows data protection).</summary>
   public sealed class MauiSecretStore : ISecretStore
   {
       public Task<string?> GetAsync(string name, CancellationToken cancellationToken) =>
           SecureStorage.Default.GetAsync(name);

       public Task SetAsync(string name, string value, CancellationToken cancellationToken) =>
           SecureStorage.Default.SetAsync(name, value);

       public Task RemoveAsync(string name, CancellationToken cancellationToken)
       {
           SecureStorage.Default.Remove(name);
           return Task.CompletedTask;
       }
   }
   ```

   `ImageGen/ImageGen.Maui/Services/AppInitializer.cs`
   ```csharp
   using ImageGen.Core.Services;
   using ImageGen.Data;

   namespace ImageGen.Maui.Services;

   /// <summary>Runs one-time startup work: creates or migrates the database and marks interrupted generations.</summary>
   public sealed class AppInitializer(SqliteDatabase database, GenerationService generationService)
   {
       private readonly Lock _lock = new();
       private Task? _initialization;

       public Task EnsureInitializedAsync()
       {
           lock (_lock)
           {
               return _initialization ??= InitializeAsync();
           }
       }

       private async Task InitializeAsync()
       {
           await database.InitializeAsync(CancellationToken.None);
           await generationService.RecoverInterruptedAsync(CancellationToken.None);
       }
   }
   ```

   `ImageGen/ImageGen.Maui/Services/IDialogService.cs`
   ```csharp
   namespace ImageGen.Maui.Services;

   public interface IDialogService
   {
       Task AlertAsync(string title, string message);

       Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);

       /// <summary>Returns the entered text, or null when the user cancelled.</summary>
       Task<string?> PromptAsync(string title, string message, string accept, string cancel, string initialValue);
   }
   ```

   `ImageGen/ImageGen.Maui/Services/ShellDialogService.cs`
   ```csharp
   using ImageGen.Maui.Resources.Strings;

   namespace ImageGen.Maui.Services;

   public sealed class ShellDialogService : IDialogService
   {
       public Task AlertAsync(string title, string message) =>
           Shell.Current.DisplayAlertAsync(title, message, AppResources.Ok);

       public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
           Shell.Current.DisplayAlertAsync(title, message, accept, cancel);

       public Task<string?> PromptAsync(string title, string message, string accept, string cancel, string initialValue) =>
           Shell.Current.DisplayPromptAsync(title, message, accept, cancel, initialValue: initialValue, maxLength: 4000);
   }
   ```

   `ImageGen/ImageGen.Maui/Services/INavigationService.cs`
   ```csharp
   namespace ImageGen.Maui.Services;

   public interface INavigationService
   {
       Task GoToAsync(string route);

       Task GoBackAsync();
   }
   ```

   `ImageGen/ImageGen.Maui/Services/ShellNavigationService.cs`
   ```csharp
   namespace ImageGen.Maui.Services;

   public sealed class ShellNavigationService : INavigationService
   {
       public Task GoToAsync(string route) => Shell.Current.GoToAsync(route);

       public Task GoBackAsync() => Shell.Current.GoToAsync("..");
   }
   ```

   `ImageGen/ImageGen.Maui/Routes.cs`
   ```csharp
   namespace ImageGen.Maui;

   public static class Routes
   {
       public const string ApiKey = "apikey";
       public const string Chat = "chat";
       public const string SessionModels = "models";
       public const string SessionIdParameter = "sessionId";

       public static string ChatFor(Guid sessionId) => $"{Chat}?{SessionIdParameter}={sessionId:D}";

       public static string SessionModelsFor(Guid sessionId) => $"{SessionModels}?{SessionIdParameter}={sessionId:D}";
   }
   ```

   `ImageGen/ImageGen.Maui/Utilities/TaskExtensions.cs`
   ```csharp
   using System.Diagnostics;

   namespace ImageGen.Maui.Utilities;

   public static class TaskExtensions
   {
       /// <summary>
       /// Runs a task from a synchronous event handler (for example <c>OnAppearing</c>) without losing exceptions.
       /// This is the only allowed place for <c>async void</c>.
       /// </summary>
       public static async void FireAndForgetSafeAsync(this Task task)
       {
           try
           {
               await task;
           }
           catch (Exception ex)
           {
               Debug.WriteLine($"Unhandled background exception: {ex}");
           }
       }
   }
   ```

3. Заменить содержимое `ImageGen/ImageGen.Maui/MauiProgram.cs` (дословно; строки страниц добавят задачи 013–015 после комментария `// Pages and page models`):
   ```csharp
   using CommunityToolkit.Maui;
   using ImageGen.Core;
   using ImageGen.Core.Platform;
   using ImageGen.Data;
   using ImageGen.Maui.Services;
   using Microsoft.Extensions.Logging;

   namespace ImageGen.Maui;

   public static class MauiProgram
   {
       public static MauiApp CreateMauiApp()
       {
           var builder = MauiApp.CreateBuilder();
           builder
               .UseMauiApp<App>()
               .UseMauiCommunityToolkit()
               .ConfigureFonts(fonts =>
               {
                   fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                   fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
               });

   #if DEBUG
           builder.Logging.AddDebug();
   #endif

           builder.Services.AddSingleton<IAppPaths, MauiAppPaths>();
           builder.Services.AddSingleton<ISecretStore, MauiSecretStore>();
           builder.Services.AddImageGenCore();
           builder.Services.AddImageGenData();

           builder.Services.AddSingleton<AppInitializer>();
           builder.Services.AddSingleton<IDialogService, ShellDialogService>();
           builder.Services.AddSingleton<INavigationService, ShellNavigationService>();

           // Pages and page models

           return builder.Build();
       }
   }
   ```

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
