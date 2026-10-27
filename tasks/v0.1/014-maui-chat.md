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

## Критерии готовности
- Файлы из плана созданы с содержимым как в плане; `MauiProgram.cs` изменён как в плане.
- Сборка под Windows и Android без ошибок и предупреждений; `dotnet format` для MAUI-проекта проходит.
- Ручная проверка (задача 016): сообщение «draw a red fox» даёт прогон с карточкой, карточка переходит из «Queued/Generating...» в «Done» с картинкой без перезагрузки страницы.

## Анализ
- Спецификация: S2, S3, S4 (`docs/specs/chat-sessions.md`).
- Строки ленты разных типов выбирает `ChatItemTemplateSelector`. Команды карточки лежат в `GenerationItemViewModel` и вызывают методы `ChatPageModel` (компилируемые привязки без ссылок на родителя).
- После своих действий (Send, Repeat, New seed, Edit) страница перечитывает всю ленту; событие обновляет только одну карточку. Подписка на событие снимается при уходе со страницы.
- Edit использует `DisplayPromptAsync` с точным промптом генерации; пустой ввод или Cancel ничего не запускают.
- Маршрут `models` регистрирует задача 015; до неё кнопка моделей упадёт с ошибкой навигации (ожидаемо).
- Код проверен сборкой под Windows и Android 2026-10-27 в пробной копии решения.

## План
1. Создать файлы (содержимое дословно):

   `ImageGen/ImageGen.Maui/PageModels/MessageItemViewModel.cs`
   ```csharp
   using ImageGen.Core.Domain;
   using ImageGen.Maui.Resources.Strings;

   namespace ImageGen.Maui.PageModels;

   public sealed class MessageItemViewModel(ChatMessage message)
   {
       public string RoleText { get; } = message.Role == ChatRole.User ? AppResources.UserRole : AppResources.AssistantRole;

       public string Text { get; } = message.Text;

       public bool IsUser { get; } = message.Role == ChatRole.User;

       public bool IsError { get; } = message.IsError;
   }
   ```

   `ImageGen/ImageGen.Maui/PageModels/GenerationItemViewModel.cs`
   ```csharp
   using System.Globalization;
   using CommunityToolkit.Mvvm.ComponentModel;
   using CommunityToolkit.Mvvm.Input;
   using ImageGen.Core.Domain;
   using ImageGen.Core.Storage;
   using ImageGen.Maui.Resources.Strings;

   namespace ImageGen.Maui.PageModels;

   /// <summary>Card of one generation. Commands call back into the owning page model.</summary>
   public sealed partial class GenerationItemViewModel : ObservableObject
   {
       private readonly IImageFileStore _files;

       public GenerationItemViewModel(
           Generation generation,
           IImageFileStore files,
           Func<GenerationItemViewModel, Task> repeat,
           Func<GenerationItemViewModel, Task> newSeed,
           Func<GenerationItemViewModel, Task> edit)
       {
           _files = files;
           Id = generation.Id;
           Prompt = generation.Prompt;
           RepeatCommand = new AsyncRelayCommand(() => repeat(this), () => IsFinished);
           NewSeedCommand = new AsyncRelayCommand(() => newSeed(this), () => IsFinished);
           EditCommand = new AsyncRelayCommand(() => edit(this), () => IsFinished);
           Update(generation);
       }

       public Guid Id { get; }

       /// <summary>Exact prompt of the generation, used by the Edit action.</summary>
       public string Prompt { get; }

       public IAsyncRelayCommand RepeatCommand { get; }

       public IAsyncRelayCommand NewSeedCommand { get; }

       public IAsyncRelayCommand EditCommand { get; }

       [ObservableProperty]
       public partial ImageSource? Image { get; set; }

       [ObservableProperty]
       public partial bool HasImage { get; set; }

       [ObservableProperty]
       public partial bool IsFinished { get; set; }

       [ObservableProperty]
       public partial bool IsRunning { get; set; }

       [ObservableProperty]
       public partial string StatusText { get; set; } = string.Empty;

       [ObservableProperty]
       public partial string DetailsText { get; set; } = string.Empty;

       [ObservableProperty]
       public partial string ErrorText { get; set; } = string.Empty;

       /// <summary>Applies the latest saved state of the generation. Call on the UI thread.</summary>
       public void Update(Generation generation)
       {
           StatusText = generation.Status switch
           {
               GenerationStatus.Queued => AppResources.StatusQueued,
               GenerationStatus.Running => AppResources.StatusRunning,
               GenerationStatus.Succeeded => AppResources.StatusSucceeded,
               GenerationStatus.Failed => AppResources.StatusFailed,
               GenerationStatus.Cancelled => AppResources.StatusCancelled,
               GenerationStatus.Filtered => AppResources.StatusFiltered,
               _ => generation.Status.ToString(),
           };

           var seedText = generation.SeedApplied
               ? string.Format(CultureInfo.CurrentCulture, AppResources.SeedFormat, generation.Seed)
               : AppResources.SeedNotSupported;
           var costText = generation.CostUsd.HasValue
               ? " · " + string.Format(CultureInfo.CurrentCulture, AppResources.CostFormat, generation.CostUsd.Value.ToString("0.####", CultureInfo.InvariantCulture))
               : string.Empty;
           DetailsText = $"{generation.ModelId} · {seedText}{costText}";
           ErrorText = generation.ErrorMessage ?? string.Empty;

           HasImage = generation.Status == GenerationStatus.Succeeded && generation.ImagePath is not null;
           Image = HasImage ? ImageSource.FromFile(_files.GetFullPath(generation.ImagePath!)) : null;
           IsRunning = generation.Status is GenerationStatus.Queued or GenerationStatus.Running;
           IsFinished = generation.IsFinished;
           RepeatCommand.NotifyCanExecuteChanged();
           NewSeedCommand.NotifyCanExecuteChanged();
           EditCommand.NotifyCanExecuteChanged();
       }
   }
   ```

   `ImageGen/ImageGen.Maui/PageModels/RunItemViewModel.cs`
   ```csharp
   using System.Collections.ObjectModel;
   using System.Globalization;
   using ImageGen.Core.Domain;
   using ImageGen.Maui.Resources.Strings;

   namespace ImageGen.Maui.PageModels;

   public sealed class RunItemViewModel(Run run, IEnumerable<GenerationItemViewModel> generations)
   {
       public Guid Id { get; } = run.Id;

       public string PromptText { get; } = string.Format(CultureInfo.CurrentCulture, AppResources.RunPromptFormat, run.Prompt);

       public ObservableCollection<GenerationItemViewModel> Generations { get; } = new(generations);
   }
   ```

   `ImageGen/ImageGen.Maui/PageModels/ChatItemTemplateSelector.cs`
   ```csharp
   namespace ImageGen.Maui.PageModels;

   /// <summary>Chooses the chat row template: message or run.</summary>
   public sealed class ChatItemTemplateSelector : DataTemplateSelector
   {
       public DataTemplate? MessageTemplate { get; set; }

       public DataTemplate? RunTemplate { get; set; }

       protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
           (item is RunItemViewModel ? RunTemplate : MessageTemplate)
           ?? throw new InvalidOperationException("Chat templates are not set.");
   }
   ```

   `ImageGen/ImageGen.Maui/PageModels/ChatPageModel.cs`
   ```csharp
   using System.Collections.ObjectModel;
   using System.Globalization;
   using CommunityToolkit.Mvvm.ComponentModel;
   using CommunityToolkit.Mvvm.Input;
   using ImageGen.Core.Services;
   using ImageGen.Core.Storage;
   using ImageGen.Maui.Resources.Strings;
   using ImageGen.Maui.Services;

   namespace ImageGen.Maui.PageModels;

   public sealed partial class ChatPageModel(
       SessionService sessionService,
       AssistantService assistantService,
       GenerationService generationService,
       IImageFileStore files,
       INavigationService navigation,
       IDialogService dialogs) : ObservableObject, IQueryAttributable
   {
       private readonly Dictionary<Guid, GenerationItemViewModel> _generations = [];
       private Guid _sessionId;

       /// <summary>Rows of the chat: <see cref="MessageItemViewModel"/> or <see cref="RunItemViewModel"/>.</summary>
       public ObservableCollection<object> Items { get; } = [];

       [ObservableProperty]
       public partial string Title { get; set; } = string.Empty;

       [ObservableProperty]
       public partial string ModelsButtonText { get; set; } = string.Empty;

       [ObservableProperty]
       [NotifyCanExecuteChangedFor(nameof(SendCommand))]
       public partial string InputText { get; set; } = string.Empty;

       [ObservableProperty]
       [NotifyCanExecuteChangedFor(nameof(SendCommand))]
       public partial bool IsBusy { get; set; }

       public void ApplyQueryAttributes(IDictionary<string, object> query)
       {
           if (query.TryGetValue(Routes.SessionIdParameter, out var value)
               && Guid.TryParse(value?.ToString(), out var sessionId))
           {
               _sessionId = sessionId;
           }
       }

       /// <summary>Called by the page on every appearance: subscribes to updates and reloads the chat.</summary>
       public async Task OnAppearingAsync()
       {
           generationService.GenerationUpdated -= OnGenerationUpdated;
           generationService.GenerationUpdated += OnGenerationUpdated;
           await ReloadAsync();
       }

       /// <summary>Called by the page when it disappears.</summary>
       public void OnDisappearing() => generationService.GenerationUpdated -= OnGenerationUpdated;

       private async Task ReloadAsync()
       {
           try
           {
               var timeline = await sessionService.GetTimelineAsync(_sessionId, CancellationToken.None);
               Title = timeline.Session.Title;
               ModelsButtonText = string.Format(
                   CultureInfo.CurrentCulture,
                   AppResources.ModelsButtonFormat,
                   timeline.Session.Settings.ImageModelIds.Count,
                   timeline.Session.Settings.RunsPerModel);

               _generations.Clear();
               Items.Clear();
               foreach (var entry in TimelineOrdering.Order(timeline))
               {
                   if (entry.Message is not null)
                   {
                       Items.Add(new MessageItemViewModel(entry.Message));
                   }
                   else if (entry.Run is not null)
                   {
                       var cards = entry.Generations
                           .Select(g => new GenerationItemViewModel(g, files, RepeatAsync, NewSeedAsync, EditAsync))
                           .ToList();
                       foreach (var card in cards)
                       {
                           _generations[card.Id] = card;
                       }

                       Items.Add(new RunItemViewModel(entry.Run, cards));
                   }
               }
           }
           catch (Exception ex)
           {
               await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
           }
       }

       private void OnGenerationUpdated(object? sender, GenerationUpdatedEventArgs e)
       {
           if (e.Generation.SessionId != _sessionId)
           {
               return;
           }

           MainThread.BeginInvokeOnMainThread(() =>
           {
               if (_generations.TryGetValue(e.Generation.Id, out var card))
               {
                   card.Update(e.Generation);
               }
           });
       }

       private bool CanSend() => !IsBusy && !string.IsNullOrWhiteSpace(InputText);

       [RelayCommand(CanExecute = nameof(CanSend))]
       private async Task SendAsync()
       {
           var text = InputText;
           IsBusy = true;
           try
           {
               InputText = string.Empty;
               await assistantService.SendUserMessageAsync(_sessionId, text, CancellationToken.None);
           }
           catch (Exception ex)
           {
               InputText = text;
               await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
           }
           finally
           {
               IsBusy = false;
           }

           await ReloadAsync();
       }

       [RelayCommand]
       private Task OpenModelsAsync() => navigation.GoToAsync(Routes.SessionModelsFor(_sessionId));

       private Task RepeatAsync(GenerationItemViewModel card) =>
           RunActionAsync(() => generationService.RepeatAsync(card.Id, CancellationToken.None));

       private Task NewSeedAsync(GenerationItemViewModel card) =>
           RunActionAsync(() => generationService.NewSeedAsync(card.Id, CancellationToken.None));

       private async Task EditAsync(GenerationItemViewModel card)
       {
           var prompt = await dialogs.PromptAsync(
               AppResources.EditPromptTitle,
               AppResources.EditPromptMessage,
               AppResources.Generate,
               AppResources.Cancel,
               card.Prompt);
           if (string.IsNullOrWhiteSpace(prompt))
           {
               return;
           }

           await RunActionAsync(() => generationService.EditAsync(card.Id, prompt, CancellationToken.None));
       }

       private async Task RunActionAsync(Func<Task> action)
       {
           try
           {
               await action();
           }
           catch (Exception ex)
           {
               await dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message);
           }

           await ReloadAsync();
       }
   }
   ```

   `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`
   ```xml
   <?xml version="1.0" encoding="utf-8" ?>
   <ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                xmlns:pm="clr-namespace:ImageGen.Maui.PageModels"
                xmlns:strings="clr-namespace:ImageGen.Maui.Resources.Strings"
                x:Class="ImageGen.Maui.Pages.ChatPage"
                x:DataType="pm:ChatPageModel"
                Title="{Binding Title}">
       <ContentPage.Resources>
           <DataTemplate x:Key="MessageTemplate" x:DataType="pm:MessageItemViewModel">
               <VerticalStackLayout Padding="0,6" Spacing="2" SemanticProperties.Description="{Binding Text}">
                   <Label Text="{Binding RoleText}" FontAttributes="Bold" FontSize="12" />
                   <Label Text="{Binding Text}" LineBreakMode="WordWrap">
                       <Label.Triggers>
                           <DataTrigger TargetType="Label" Binding="{Binding IsError}" Value="True">
                               <Setter Property="TextColor" Value="Firebrick" />
                           </DataTrigger>
                       </Label.Triggers>
                   </Label>
               </VerticalStackLayout>
           </DataTemplate>
           <DataTemplate x:Key="RunTemplate" x:DataType="pm:RunItemViewModel">
               <VerticalStackLayout Padding="0,6" Spacing="8">
                   <Label Text="{Binding PromptText}" FontAttributes="Italic" LineBreakMode="WordWrap" />
                   <FlexLayout Wrap="Wrap" BindableLayout.ItemsSource="{Binding Generations}">
                       <BindableLayout.ItemTemplate>
                           <DataTemplate x:DataType="pm:GenerationItemViewModel">
                               <Border Margin="0,0,8,8" Padding="8" WidthRequest="280" StrokeThickness="1">
                                   <VerticalStackLayout Spacing="4" SemanticProperties.Description="{Binding StatusText}">
                                       <Grid HeightRequest="256">
                                           <Image Source="{Binding Image}" IsVisible="{Binding HasImage}" Aspect="AspectFit" />
                                           <ActivityIndicator IsRunning="{Binding IsRunning}" IsVisible="{Binding IsRunning}" />
                                       </Grid>
                                       <Label Text="{Binding StatusText}" FontAttributes="Bold" />
                                       <Label Text="{Binding DetailsText}" FontSize="12" LineBreakMode="WordWrap" />
                                       <Label Text="{Binding ErrorText}" FontSize="12" TextColor="Firebrick" LineBreakMode="WordWrap" />
                                       <HorizontalStackLayout Spacing="4">
                                           <Button Text="{x:Static strings:AppResources.Repeat}" Command="{Binding RepeatCommand}"
                                                   SemanticProperties.Description="{x:Static strings:AppResources.Repeat}" />
                                           <Button Text="{x:Static strings:AppResources.NewSeed}" Command="{Binding NewSeedCommand}"
                                                   SemanticProperties.Description="{x:Static strings:AppResources.NewSeed}" />
                                           <Button Text="{x:Static strings:AppResources.Edit}" Command="{Binding EditCommand}"
                                                   SemanticProperties.Description="{x:Static strings:AppResources.Edit}" />
                                       </HorizontalStackLayout>
                                   </VerticalStackLayout>
                               </Border>
                           </DataTemplate>
                       </BindableLayout.ItemTemplate>
                   </FlexLayout>
               </VerticalStackLayout>
           </DataTemplate>
           <pm:ChatItemTemplateSelector x:Key="ChatItemSelector"
                                        MessageTemplate="{StaticResource MessageTemplate}"
                                        RunTemplate="{StaticResource RunTemplate}" />
       </ContentPage.Resources>
       <Grid RowDefinitions="Auto,*,Auto,Auto" Padding="16" RowSpacing="8">
           <Button Text="{Binding ModelsButtonText}"
                   Command="{Binding OpenModelsCommand}"
                   HorizontalOptions="Start"
                   SemanticProperties.Description="{Binding ModelsButtonText}" />
           <CollectionView Grid.Row="1"
                           ItemsSource="{Binding Items}"
                           ItemTemplate="{StaticResource ChatItemSelector}"
                           SelectionMode="None"
                           ItemsUpdatingScrollMode="KeepLastItemInView" />
           <Label Grid.Row="2" Text="{x:Static strings:AppResources.AssistantThinking}" IsVisible="{Binding IsBusy}" />
           <Grid Grid.Row="3" ColumnDefinitions="*,Auto" ColumnSpacing="8">
               <Editor Text="{Binding InputText}"
                       Placeholder="{x:Static strings:AppResources.ChatPlaceholder}"
                       AutoSize="TextChanges"
                       MaximumHeightRequest="160"
                       SemanticProperties.Description="{x:Static strings:AppResources.ChatPlaceholder}" />
               <Button Grid.Column="1"
                       Text="{x:Static strings:AppResources.Send}"
                       Command="{Binding SendCommand}"
                       VerticalOptions="End"
                       SemanticProperties.Description="{x:Static strings:AppResources.Send}" />
           </Grid>
       </Grid>
   </ContentPage>
   ```

   `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml.cs`
   ```csharp
   using ImageGen.Maui.PageModels;
   using ImageGen.Maui.Utilities;

   namespace ImageGen.Maui.Pages;

   public partial class ChatPage : ContentPage
   {
       private readonly ChatPageModel _model;

       public ChatPage(ChatPageModel model)
       {
           InitializeComponent();
           BindingContext = _model = model;
       }

       protected override void OnAppearing()
       {
           base.OnAppearing();
           _model.OnAppearingAsync().FireAndForgetSafeAsync();
       }

       protected override void OnDisappearing()
       {
           _model.OnDisappearing();
           base.OnDisappearing();
       }
   }
   ```

2. В `ImageGen/ImageGen.Maui/MauiProgram.cs` после строки `builder.Services.AddTransientWithShellRoute<ApiKeyPage, ApiKeyPageModel>(Routes.ApiKey);` добавить строку (отступ 8 пробелов):
   ```csharp
   builder.Services.AddTransientWithShellRoute<ChatPage, ChatPageModel>(Routes.Chat);
   ```

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
