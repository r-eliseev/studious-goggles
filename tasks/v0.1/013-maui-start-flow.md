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

## Критерии готовности
- Файлы из плана созданы с содержимым как в плане; `MainPage.xaml` и `MainPage.xaml.cs` удалены; `MauiProgram.cs` изменён как в плане.
- Сборка под Windows и Android без ошибок и предупреждений; `dotnet format` для MAUI-проекта проходит.
- Ручная проверка на Windows (делает человек, задача 016): при первом запуске открывается страница ключа; после сохранения верного ключа — пустой список сессий.

## Анализ
- Спецификация: S1.1–S1.2 (`docs/specs/chat-sessions.md`), F1.1–F1.3 и F10.6 (предупреждение, куда уходят промпты, — в тексте `ApiKeyDescription`).
- Команды строк списка лежат в `SessionItemViewModel` и вызывают методы владельца: так привязки в шаблоне остаются компилируемыми (`x:DataType`) без ссылок на родительскую страницу.
- Маршрут `chat` регистрирует задача 014; до неё кнопка «New session» создаёт сессию, но переход упадёт с ошибкой навигации (ожидаемо).
- Код проверен сборкой под Windows и Android и запуском под Windows 2026-10-27 в пробной копии решения (приложение стартует и создаёт `imagegen.db`).

## План
1. Удалить `ImageGen/ImageGen.Maui/MainPage.xaml` и `ImageGen/ImageGen.Maui/MainPage.xaml.cs`.

2. Заменить содержимое `ImageGen/ImageGen.Maui/AppShell.xaml` (дословно):

   `ImageGen/ImageGen.Maui/AppShell.xaml`
   ```xml
   <?xml version="1.0" encoding="UTF-8" ?>
   <Shell
       x:Class="ImageGen.Maui.AppShell"
       xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
       xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
       xmlns:pages="clr-namespace:ImageGen.Maui.Pages"
       xmlns:strings="clr-namespace:ImageGen.Maui.Resources.Strings"
       Shell.FlyoutBehavior="Disabled"
       Title="{x:Static strings:AppResources.AppTitle}">

       <ShellContent
           Title="{x:Static strings:AppResources.SessionsTitle}"
           ContentTemplate="{DataTemplate pages:SessionsPage}"
           Route="sessions" />

   </Shell>
   ```

3. Создать файлы (содержимое дословно):

   `ImageGen/ImageGen.Maui/PageModels/ApiKeyPageModel.cs`
   ```csharp
   using CommunityToolkit.Mvvm.ComponentModel;
   using CommunityToolkit.Mvvm.Input;
   using ImageGen.Core.Domain;
   using ImageGen.Core.Services;
   using ImageGen.Maui.Resources.Strings;
   using ImageGen.Maui.Services;

   namespace ImageGen.Maui.PageModels;

   public sealed partial class ApiKeyPageModel(ApiKeyService apiKeyService, INavigationService navigation) : ObservableObject
   {
       [ObservableProperty]
       [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
       public partial string ApiKey { get; set; } = string.Empty;

       [ObservableProperty]
       [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
       public partial bool IsBusy { get; set; }

       [ObservableProperty]
       public partial string StatusText { get; set; } = string.Empty;

       private bool CanSave() => !IsBusy && !string.IsNullOrWhiteSpace(ApiKey);

       [RelayCommand(CanExecute = nameof(CanSave))]
       private async Task SaveAsync()
       {
           IsBusy = true;
           StatusText = AppResources.ApiKeyChecking;
           try
           {
               var status = await apiKeyService.SaveAsync(ApiKey, CancellationToken.None);
               switch (status)
               {
                   case ApiKeyStatus.Valid:
                       ApiKey = string.Empty;
                       StatusText = string.Empty;
                       await navigation.GoBackAsync();
                       break;
                   case ApiKeyStatus.Invalid:
                       StatusText = AppResources.ApiKeyInvalid;
                       break;
                   default:
                       StatusText = AppResources.ApiKeyNetworkError;
                       break;
               }

               SemanticScreenReader.Announce(StatusText);
           }
           finally
           {
               IsBusy = false;
           }
       }
   }
   ```

   `ImageGen/ImageGen.Maui/Pages/ApiKeyPage.xaml`
   ```xml
   <?xml version="1.0" encoding="utf-8" ?>
   <ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                xmlns:pm="clr-namespace:ImageGen.Maui.PageModels"
                xmlns:strings="clr-namespace:ImageGen.Maui.Resources.Strings"
                x:Class="ImageGen.Maui.Pages.ApiKeyPage"
                x:DataType="pm:ApiKeyPageModel"
                Title="{x:Static strings:AppResources.ApiKeyTitle}">
       <VerticalStackLayout Padding="24" Spacing="16" MaximumWidthRequest="600">
           <Label Text="{x:Static strings:AppResources.ApiKeyDescription}" />
           <Entry Text="{Binding ApiKey}"
                  IsPassword="True"
                  Placeholder="{x:Static strings:AppResources.ApiKeyPlaceholder}"
                  SemanticProperties.Description="{x:Static strings:AppResources.ApiKeyTitle}" />
           <Button Text="{x:Static strings:AppResources.ApiKeySave}"
                   Command="{Binding SaveCommand}"
                   SemanticProperties.Description="{x:Static strings:AppResources.ApiKeySave}" />
           <ActivityIndicator IsRunning="{Binding IsBusy}" IsVisible="{Binding IsBusy}" />
           <Label Text="{Binding StatusText}" />
       </VerticalStackLayout>
   </ContentPage>
   ```

   `ImageGen/ImageGen.Maui/Pages/ApiKeyPage.xaml.cs`
   ```csharp
   using ImageGen.Maui.PageModels;

   namespace ImageGen.Maui.Pages;

   public partial class ApiKeyPage : ContentPage
   {
       public ApiKeyPage(ApiKeyPageModel model)
       {
           InitializeComponent();
           BindingContext = model;
       }
   }
   ```

   `ImageGen/ImageGen.Maui/PageModels/SessionItemViewModel.cs`
   ```csharp
   using System.Globalization;
   using CommunityToolkit.Mvvm.Input;
   using ImageGen.Core.Domain;

   namespace ImageGen.Maui.PageModels;

   /// <summary>Row of the sessions list. Commands call back into the owning page model.</summary>
   public sealed class SessionItemViewModel
   {
       public SessionItemViewModel(
           Session session,
           Func<SessionItemViewModel, Task> open,
           Func<SessionItemViewModel, Task> delete)
       {
           Id = session.Id;
           Title = session.Title;
           UpdatedText = session.UpdatedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
           OpenCommand = new AsyncRelayCommand(() => open(this));
           DeleteCommand = new AsyncRelayCommand(() => delete(this));
       }

       public Guid Id { get; }

       public string Title { get; }

       public string UpdatedText { get; }

       public IAsyncRelayCommand OpenCommand { get; }

       public IAsyncRelayCommand DeleteCommand { get; }
   }
   ```

   `ImageGen/ImageGen.Maui/PageModels/SessionsPageModel.cs`
   ```csharp
   using System.Collections.ObjectModel;
   using CommunityToolkit.Mvvm.ComponentModel;
   using CommunityToolkit.Mvvm.Input;
   using ImageGen.Core.Services;
   using ImageGen.Maui.Resources.Strings;
   using ImageGen.Maui.Services;

   namespace ImageGen.Maui.PageModels;

   public sealed partial class SessionsPageModel(
       AppInitializer initializer,
       ApiKeyService apiKeyService,
       SessionService sessionService,
       INavigationService navigation,
       IDialogService dialogs) : ObservableObject
   {
       public ObservableCollection<SessionItemViewModel> Sessions { get; } = [];

       [ObservableProperty]
       public partial bool IsBusy { get; set; }

       /// <summary>Called by the page on every appearance.</summary>
       public async Task OnAppearingAsync()
       {
           try
           {
               IsBusy = true;
               await initializer.EnsureInitializedAsync();
               if (!await apiKeyService.HasKeyAsync(CancellationToken.None))
               {
                   await navigation.GoToAsync(Routes.ApiKey);
                   return;
               }

               var sessions = await sessionService.ListAsync(CancellationToken.None);
               Sessions.Clear();
               foreach (var session in sessions)
               {
                   Sessions.Add(new SessionItemViewModel(session, OpenSessionAsync, DeleteSessionAsync));
               }
           }
           catch (Exception ex)
           {
               await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
           }
           finally
           {
               IsBusy = false;
           }
       }

       [RelayCommand]
       private async Task NewSessionAsync()
       {
           try
           {
               var session = await sessionService.CreateAsync(CancellationToken.None);
               await navigation.GoToAsync(Routes.ChatFor(session.Id));
           }
           catch (Exception ex)
           {
               await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
           }
       }

       private Task OpenSessionAsync(SessionItemViewModel item) => navigation.GoToAsync(Routes.ChatFor(item.Id));

       private async Task DeleteSessionAsync(SessionItemViewModel item)
       {
           var confirmed = await dialogs.ConfirmAsync(
               AppResources.DeleteSessionTitle,
               AppResources.DeleteSessionMessage,
               AppResources.DeleteSession,
               AppResources.Cancel);
           if (!confirmed)
           {
               return;
           }

           try
           {
               await sessionService.DeleteAsync(item.Id, CancellationToken.None);
               Sessions.Remove(item);
           }
           catch (Exception ex)
           {
               await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
           }
       }
   }
   ```

   `ImageGen/ImageGen.Maui/Pages/SessionsPage.xaml`
   ```xml
   <?xml version="1.0" encoding="utf-8" ?>
   <ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                xmlns:pm="clr-namespace:ImageGen.Maui.PageModels"
                xmlns:strings="clr-namespace:ImageGen.Maui.Resources.Strings"
                x:Class="ImageGen.Maui.Pages.SessionsPage"
                x:DataType="pm:SessionsPageModel"
                Title="{x:Static strings:AppResources.SessionsTitle}">
       <Grid RowDefinitions="Auto,*" Padding="16" RowSpacing="12">
           <Button Text="{x:Static strings:AppResources.NewSession}"
                   Command="{Binding NewSessionCommand}"
                   HorizontalOptions="Start"
                   SemanticProperties.Description="{x:Static strings:AppResources.NewSession}" />
           <ActivityIndicator Grid.Row="1" IsRunning="{Binding IsBusy}" IsVisible="{Binding IsBusy}" VerticalOptions="Start" />
           <CollectionView Grid.Row="1" ItemsSource="{Binding Sessions}" SelectionMode="None">
               <CollectionView.ItemTemplate>
                   <DataTemplate x:DataType="pm:SessionItemViewModel">
                       <Grid ColumnDefinitions="*,Auto" Padding="0,8" ColumnSpacing="12">
                           <Grid.GestureRecognizers>
                               <TapGestureRecognizer Command="{Binding OpenCommand}" />
                           </Grid.GestureRecognizers>
                           <VerticalStackLayout SemanticProperties.Description="{Binding Title}">
                               <Label Text="{Binding Title}" FontAttributes="Bold" />
                               <Label Text="{Binding UpdatedText}" FontSize="12" />
                           </VerticalStackLayout>
                           <Button Grid.Column="1"
                                   Text="{x:Static strings:AppResources.DeleteSession}"
                                   Command="{Binding DeleteCommand}"
                                   SemanticProperties.Description="{x:Static strings:AppResources.DeleteSession}" />
                       </Grid>
                   </DataTemplate>
               </CollectionView.ItemTemplate>
           </CollectionView>
       </Grid>
   </ContentPage>
   ```

   `ImageGen/ImageGen.Maui/Pages/SessionsPage.xaml.cs`
   ```csharp
   using ImageGen.Maui.PageModels;
   using ImageGen.Maui.Utilities;

   namespace ImageGen.Maui.Pages;

   public partial class SessionsPage : ContentPage
   {
       private readonly SessionsPageModel _model;

       public SessionsPage(SessionsPageModel model)
       {
           InitializeComponent();
           BindingContext = _model = model;
       }

       protected override void OnAppearing()
       {
           base.OnAppearing();
           _model.OnAppearingAsync().FireAndForgetSafeAsync();
       }
   }
   ```

4. В `ImageGen/ImageGen.Maui/MauiProgram.cs`:
   - после строки `using ImageGen.Data;` добавить две строки `using ImageGen.Maui.PageModels;` и `using ImageGen.Maui.Pages;`;
   - после строки `// Pages and page models` добавить:
     ```csharp
     builder.Services.AddSingleton<SessionsPage>();
     builder.Services.AddSingleton<SessionsPageModel>();
     builder.Services.AddTransientWithShellRoute<ApiKeyPage, ApiKeyPageModel>(Routes.ApiKey);
     ```
     (отступ как у соседних строк `builder.Services`, 8 пробелов).

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
