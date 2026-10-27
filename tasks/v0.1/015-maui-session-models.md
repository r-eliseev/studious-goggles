---
id: 015
status: planned
assignee:
claimed:
created: 2026-10-27
---

# MAUI: модели сессии

## Описание
Экран настроек сессии: список моделей картинок OpenRouter из каталога (имя, id, «seed» или «no seed»), поиск по имени и id, галочки выбора, число запусков на модель от 1 до 10 (Stepper), кнопка Refresh (перечитать каталог из API) и Save (сохранить и вернуться в чат).

Зависит от: 014.
Среда: только Windows с установленными нагрузками MAUI (сборка под Windows и Android).

## Критерии готовности
- Файлы из плана созданы с содержимым как в плане; `MauiProgram.cs` изменён как в плане.
- Сборка под Windows и Android без ошибок и предупреждений; `dotnet format` для MAUI-проекта проходит.
- Ручная проверка (задача 016): выбранные модели и число запусков сохраняются, подпись кнопки в чате меняется на «Models: N × M», следующий прогон создаёт N × M карточек.

## Анализ
- Спецификация: S5.1–S5.2 (`docs/specs/chat-sessions.md`).
- Выбор сохраняется при поиске и при Refresh (берётся из текущих галочек). Выбранные модели, которых нет в каталоге, при сохранении не теряются.
- Порядок моделей в сессии — порядок каталога (по имени); выбор порядка позже.
- Код проверен сборкой под Windows и Android 2026-10-27 в пробной копии решения.

## План
1. Создать файлы (содержимое дословно):

   `ImageGen/ImageGen.Maui/PageModels/ModelOptionViewModel.cs`
   ```csharp
   using CommunityToolkit.Mvvm.ComponentModel;
   using ImageGen.Core.Domain;
   using ImageGen.Maui.Resources.Strings;

   namespace ImageGen.Maui.PageModels;

   public sealed partial class ModelOptionViewModel(ImageModel model, bool isSelected) : ObservableObject
   {
       public string Id { get; } = model.Id;

       public string Name { get; } = model.Name;

       public string DetailsText { get; } = model.Id + " · " + (model.SupportsSeed ? AppResources.ModelSupportsSeed : AppResources.ModelNoSeed);

       [ObservableProperty]
       public partial bool IsSelected { get; set; } = isSelected;
   }
   ```

   `ImageGen/ImageGen.Maui/PageModels/SessionModelsPageModel.cs`
   ```csharp
   using System.Collections.ObjectModel;
   using System.Globalization;
   using CommunityToolkit.Mvvm.ComponentModel;
   using CommunityToolkit.Mvvm.Input;
   using ImageGen.Core.Domain;
   using ImageGen.Core.Services;
   using ImageGen.Maui.Resources.Strings;
   using ImageGen.Maui.Services;

   namespace ImageGen.Maui.PageModels;

   public sealed partial class SessionModelsPageModel(
       SessionService sessionService,
       ModelCatalogService catalog,
       INavigationService navigation,
       IDialogService dialogs) : ObservableObject, IQueryAttributable
   {
       private readonly List<ModelOptionViewModel> _all = [];
       private Session? _session;
       private Guid _sessionId;

       public ObservableCollection<ModelOptionViewModel> Models { get; } = [];

       [ObservableProperty]
       public partial string SearchText { get; set; } = string.Empty;

       [ObservableProperty]
       [NotifyPropertyChangedFor(nameof(RunsPerModelText))]
       public partial int RunsPerModel { get; set; } = SessionSettings.MinRunsPerModel;

       [ObservableProperty]
       public partial bool IsBusy { get; set; }

       public string RunsPerModelText =>
           string.Format(CultureInfo.CurrentCulture, AppResources.RunsPerModelFormat, RunsPerModel);

       public int MinRunsPerModel => SessionSettings.MinRunsPerModel;

       public int MaxRunsPerModel => SessionSettings.MaxRunsPerModel;

       public void ApplyQueryAttributes(IDictionary<string, object> query)
       {
           if (query.TryGetValue(Routes.SessionIdParameter, out var value)
               && Guid.TryParse(value?.ToString(), out var sessionId))
           {
               _sessionId = sessionId;
           }
       }

       /// <summary>Called by the page when it appears for the first time.</summary>
       public Task OnAppearingAsync() => LoadAsync(forceRefresh: false);

       [RelayCommand]
       private Task RefreshAsync() => LoadAsync(forceRefresh: true);

       private async Task LoadAsync(bool forceRefresh)
       {
           IsBusy = true;
           try
           {
               _session = await sessionService.GetAsync(_sessionId, CancellationToken.None)
                   ?? throw new InvalidOperationException($"Session {_sessionId} not found.");
               var selected = _all.Count > 0
                   ? _all.Where(m => m.IsSelected).Select(m => m.Id).ToHashSet()
                   : _session.Settings.ImageModelIds.ToHashSet();
               if (_all.Count == 0)
               {
                   RunsPerModel = _session.Settings.RunsPerModel;
               }

               var models = await catalog.GetImageModelsAsync(forceRefresh, CancellationToken.None);
               _all.Clear();
               _all.AddRange(models.Select(m => new ModelOptionViewModel(m, selected.Contains(m.Id))));
               ApplyFilter();
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

       partial void OnSearchTextChanged(string value) => ApplyFilter();

       private void ApplyFilter()
       {
           var search = SearchText.Trim();
           Models.Clear();
           foreach (var model in _all)
           {
               if (search.Length == 0
                   || model.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                   || model.Id.Contains(search, StringComparison.OrdinalIgnoreCase))
               {
                   Models.Add(model);
               }
           }
       }

       [RelayCommand]
       private async Task SaveAsync()
       {
           if (_session is null)
           {
               return;
           }

           try
           {
               var knownIds = _all.Select(m => m.Id).ToHashSet();
               var unknownSelected = _session.Settings.ImageModelIds.Where(id => !knownIds.Contains(id));
               var settings = new SessionSettings
               {
                   ImageModelIds = _all.Where(m => m.IsSelected).Select(m => m.Id).Concat(unknownSelected).ToList(),
                   RunsPerModel = RunsPerModel,
                   AssistantModelId = _session.Settings.AssistantModelId,
               };
               await sessionService.UpdateSettingsAsync(_sessionId, settings, CancellationToken.None);
               await navigation.GoBackAsync();
           }
           catch (Exception ex)
           {
               await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
           }
       }
   }
   ```

   `ImageGen/ImageGen.Maui/Pages/SessionModelsPage.xaml`
   ```xml
   <?xml version="1.0" encoding="utf-8" ?>
   <ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                xmlns:pm="clr-namespace:ImageGen.Maui.PageModels"
                xmlns:strings="clr-namespace:ImageGen.Maui.Resources.Strings"
                x:Class="ImageGen.Maui.Pages.SessionModelsPage"
                x:DataType="pm:SessionModelsPageModel"
                Title="{x:Static strings:AppResources.ModelsTitle}">
       <Grid RowDefinitions="Auto,Auto,*,Auto" Padding="16" RowSpacing="8">
           <SearchBar Text="{Binding SearchText}"
                      Placeholder="{x:Static strings:AppResources.ModelsSearchPlaceholder}"
                      SemanticProperties.Description="{x:Static strings:AppResources.ModelsSearchPlaceholder}" />
           <HorizontalStackLayout Grid.Row="1" Spacing="12">
               <Label Text="{Binding RunsPerModelText}" VerticalOptions="Center" />
               <Stepper Value="{Binding RunsPerModel}"
                        Minimum="{Binding MinRunsPerModel}"
                        Maximum="{Binding MaxRunsPerModel}"
                        Increment="1"
                        SemanticProperties.Description="{Binding RunsPerModelText}" />
               <Button Text="{x:Static strings:AppResources.Refresh}"
                       Command="{Binding RefreshCommand}"
                       SemanticProperties.Description="{x:Static strings:AppResources.Refresh}" />
           </HorizontalStackLayout>
           <ActivityIndicator Grid.Row="2" IsRunning="{Binding IsBusy}" IsVisible="{Binding IsBusy}" VerticalOptions="Start" />
           <CollectionView Grid.Row="2" ItemsSource="{Binding Models}" SelectionMode="None">
               <CollectionView.ItemTemplate>
                   <DataTemplate x:DataType="pm:ModelOptionViewModel">
                       <Grid ColumnDefinitions="Auto,*" ColumnSpacing="8" Padding="0,4">
                           <CheckBox IsChecked="{Binding IsSelected}" SemanticProperties.Description="{Binding Name}" />
                           <VerticalStackLayout Grid.Column="1" VerticalOptions="Center">
                               <Label Text="{Binding Name}" />
                               <Label Text="{Binding DetailsText}" FontSize="12" />
                           </VerticalStackLayout>
                       </Grid>
                   </DataTemplate>
               </CollectionView.ItemTemplate>
           </CollectionView>
           <Button Grid.Row="3"
                   Text="{x:Static strings:AppResources.Save}"
                   Command="{Binding SaveCommand}"
                   SemanticProperties.Description="{x:Static strings:AppResources.Save}" />
       </Grid>
   </ContentPage>
   ```

   `ImageGen/ImageGen.Maui/Pages/SessionModelsPage.xaml.cs`
   ```csharp
   using ImageGen.Maui.PageModels;
   using ImageGen.Maui.Utilities;

   namespace ImageGen.Maui.Pages;

   public partial class SessionModelsPage : ContentPage
   {
       private readonly SessionModelsPageModel _model;
       private bool _loaded;

       public SessionModelsPage(SessionModelsPageModel model)
       {
           InitializeComponent();
           BindingContext = _model = model;
       }

       protected override void OnAppearing()
       {
           base.OnAppearing();
           if (_loaded)
           {
               return;
           }

           _loaded = true;
           _model.OnAppearingAsync().FireAndForgetSafeAsync();
       }
   }
   ```

2. В `ImageGen/ImageGen.Maui/MauiProgram.cs` после строки `builder.Services.AddTransientWithShellRoute<ChatPage, ChatPageModel>(Routes.Chat);` добавить строку (отступ 8 пробелов):
   ```csharp
   builder.Services.AddTransientWithShellRoute<SessionModelsPage, SessionModelsPageModel>(Routes.SessionModels);
   ```
   После этого файл должен совпадать с итоговым:

   `ImageGen/ImageGen.Maui/MauiProgram.cs`
   ```csharp
   using CommunityToolkit.Maui;
   using ImageGen.Core;
   using ImageGen.Core.Platform;
   using ImageGen.Data;
   using ImageGen.Maui.PageModels;
   using ImageGen.Maui.Pages;
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
           builder.Services.AddSingleton<SessionsPage>();
           builder.Services.AddSingleton<SessionsPageModel>();
           builder.Services.AddTransientWithShellRoute<ApiKeyPage, ApiKeyPageModel>(Routes.ApiKey);
           builder.Services.AddTransientWithShellRoute<ChatPage, ChatPageModel>(Routes.Chat);
           builder.Services.AddTransientWithShellRoute<SessionModelsPage, SessionModelsPageModel>(Routes.SessionModels);

           return builder.Build();
       }
   }
   ```

Файлы: `ImageGen/ImageGen.Maui/MauiProgram.cs`, `ImageGen/ImageGen.Maui/PageModels/ModelOptionViewModel.cs`, `ImageGen/ImageGen.Maui/PageModels/SessionModelsPageModel.cs`, `ImageGen/ImageGen.Maui/Pages/SessionModelsPage.*`

### Проверка
```
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
scripts/check.sh
```

## Отчёт

## Комментарии
