# Паттерны проекта-примера `ImageGen/`

Источник: чтение каждого файла в `ImageGen/` (кроме бинарных шрифтов и сгенерированного `Resources/Fonts/FluentUI.cs`, это список глифов иконок). Дата чтения: 2026-10-27. Пример — MAUI-шаблон «проекты и задачи» (`Project`, `ProjectTask`, `Category`, `Tag`) под именем ImageGen; генерации изображений в нём нет. Сборка проекта в этой сессии не запускалась: описано то, что видно в коде.

Раздел «Брать» — паттерны, которые стоит повторить. «Не переносить» — дефекты и приёмы, которые не копировать.

## Состав решения
- `ImageGen/ImageGen.slnx` — решение в новом формате `.slnx`, один проект.
- `ImageGen/ImageGen/ImageGen.csproj` — единственный проект (подробно ниже).
- `ImageGen/ImageGen/.gitignore` — `bin/`, `obj/`, `.vs/`, `*.user`, `*.suo`, `.DS_Store`.
- `Properties/launchSettings.json` — профиль «Windows Machine».

## ImageGen.csproj
- `TargetFrameworks`: `net10.0-android` всегда; `net10.0-ios` и `net10.0-maccatalyst` если ОС не Linux; `net10.0-windows10.0.19041.0` только на Windows. Условия через `$([MSBuild]::IsOSPlatform(...))`. Linux-цели нет.
- `OutputType=Exe`, `UseMaui=true`, `SingleProject=true`, `ImplicitUsings`, `Nullable` включены.
- `MauiXamlInflator=SourceGen` и `MauiEnableXamlCBindingWithSourceCompilation=true`: XAML компилируется в C#, привязки компилируемые.
- `WindowsPackageType=None` (неупакованное Windows-приложение).
- Минимальные платформы: iOS 15.0, Mac Catalyst 17.0, Android 21.0, Windows 10.0.17763.0.
- `ApplicationId=com.companyname.imagegen` — placeholder.
- Ресурсы: `MauiIcon`, `MauiSplashScreen`, `MauiImage Resources\Images\*` (каталога нет), `MauiFont Resources\Fonts\*`, `MauiAsset Resources\Raw\**` с `LogicalName`, чтобы читать через `FileSystem.OpenAppPackageFileAsync("SeedData.json")`.
- Пакеты: `Microsoft.Maui.Controls $(MauiVersion)`, `Microsoft.Extensions.Logging.Debug 10.0.0`, `CommunityToolkit.Mvvm 8.4.2`, `CommunityToolkit.Maui 12.3.0`, `Microsoft.Data.Sqlite.Core 8.0.8`, `SQLitePCLRaw.bundle_e_sqlite3 3.0.3`, `Syncfusion.Maui.Toolkit 1.0.10`.
- Брать: условные TFM, SourceGen, явные версии пакетов. Не переносить: placeholder `ApplicationId`; `MauiImage` на несуществующий каталог.

## Точка входа и DI: `MauiProgram.cs`, `App.xaml(.cs)`, `AppShell.xaml(.cs)`, `GlobalUsings.cs`
- `MauiProgram.CreateMauiApp`: `UseMauiApp<App>()`, `UseMauiCommunityToolkit()`, `ConfigureSyncfusionToolkit()`, `ConfigureFonts` (OpenSans, SegoeUI, FluentSystemIcons).
- Под `#if WINDOWS` правки обработчиков: `SingleSelectionFollowsFocus=false` у `CollectionView` (доступность с клавиатуры), `IsTabStop=true` у `CategoryChart`.
- Под `#if DEBUG` — `AddDebug()` логирования.
- Регистрации: репозитории, `SeedDataService`, `ModalErrorHandler` и три «основных» PageModel — `AddSingleton`; детальные страницы — `AddTransientWithShellRoute<ProjectDetailPage, ProjectDetailPageModel>("project")` и то же для `task` (расширение CommunityToolkit.Maui, регистрирует страницу, модель и маршрут Shell).
- `App.CreateWindow` возвращает `new Window(new AppShell())` (современный способ вместо `MainPage`).
- `AppShell.xaml`: `Shell` с `FlyoutBehavior=Flyout`, три `ShellContent` с `Route`, `ContentTemplate="{DataTemplate pages:MainPage}"` (страницы создаются через DI), иконки из `FontImageSource` ресурсов, `SemanticProperties` у каждого пункта; в `FlyoutFooter` переключатель темы (`SfSegmentedControl`), по выбору ставит `Application.Current.UserAppTheme`.
- `AppShell.xaml.cs`: статические `DisplaySnackbarAsync` и `DisplayToastAsync` на CommunityToolkit.Maui; на Windows тост пропускается (`OperatingSystem.IsWindows()`, в коде комментарий, что Toast там не работает).
- `GlobalUsings.cs`: глобальные using своих пространств (`Fonts`, `Data`, `PageModels`, `Pages`, `Services`, `Utilities`).
- Брать: DI-регистрация детальных страниц через `AddTransientWithShellRoute`, `CreateWindow`, платформенные `#if` только в `MauiProgram`/`Platforms`. Не переносить: список singleton для PageModel без осознанной причины (состояние сохраняется между показами; в примере его гасит `Appearing`-логика).

## Данные: `Data/`
- `Constants.cs`: путь БД `Data Source=<FileSystem.AppDataDirectory>/AppSQLite.db3`. Зависит от MAUI Essentials, поэтому в Core не переносится.
- `*Repository.cs` (Category, Project, Tag, Task): чистый ADO.NET на `Microsoft.Data.Sqlite`. Каждый метод открывает своё `SqliteConnection` (`await using`), параметры `@name`, ленивая `Init()` с флагом `_hasBeenInitialized` создаёт таблицу `CREATE TABLE IF NOT EXISTS`. CRUD: `ListAsync`, `GetAsync`, `SaveItemAsync` (INSERT + `SELECT last_insert_rowid()` при `ID==0`, иначе UPDATE), `DeleteItemAsync`, `DropTableAsync`. Журнал через `ILogger<T>`. XML-комментарии на публичных методах.
- `TagRepository` дополнительно ведёт таблицу связи `ProjectsTags` (составной ключ), методы с `projectID` и проверка `IsAssociated`.
- `ProjectRepository.ListAsync`/`GetAsync` после чтения проектов для каждого грузит теги и задачи отдельными запросами.
- `TaskRespository.cs` — файл с опечаткой в имени; класс `TaskRepository`.
- `SeedDataService.cs`: читает `SeedData.json` из пакета (`FileSystem.OpenAppPackageFileAsync`), разбирает вручную через `JsonDocument` (нет рефлексии), проверяет обязательные поля (`GetRequiredString`), под `SemaphoreSlim` очищает таблицы и заполняет заново.
- `JsonContext.cs`: `JsonSerializerContext` с `[JsonSerializable]` для моделей (source generation); в самом сидировании не используется.
- `Resources/Raw/SeedData.json`: стартовые данные (проекты с категорией, тегами, задачами).
- Брать: параметризованный SQL, ленивую инициализацию схемы, `SemaphoreSlim` вокруг записи в общее состояние, валидацию входного JSON с понятным сообщением, source-generated JSON. Не переносить: дублирование CRUD в каждом репозитории (вынести общее), открытие нового соединения на каждую операцию без причины, N+1 в `ProjectRepository`, ручное чтение по порядковым индексам колонок (`reader.GetInt32(0)`: ломается при смене схемы), `DropTable` в рабочих сценариях, отсутствие интерфейсов (мешает тестам), отсутствие миграций.

## Модели: `Models/`
- `Project`, `ProjectTask`, `Category`, `Tag`, `ProjectsTags` (не используется), `ProjectsJson` (корень JSON), `CategoryChartData`, `IconData`.
- Свойства `ID`, `Name`/`Title`, `Description`, `Icon` и так далее; коллекции инициализированы `[]`.
- `[JsonIgnore]` на внутренних полях (`CategoryID`, `ProjectID`, UI-свойства).
- `Category.ColorBrush`, `Tag.ColorBrush/DisplayColor/DisplayDarkColor/DisplayLightColor/IsSelected` — вычисляемые UI-свойства (`Brush`, `Color`) прямо в модели; `Project.AccessibilityDescription` — текст для скринридера.
- Брать: инициализацию коллекций, `ToString()` для отображения в `Picker`, отдельную модель корня JSON. Не переносить: UI-типы в моделях (в Core нельзя; делать конвертер или свойство в PageModel).

## Сервисы и утилиты: `Services/`, `Utilities/`
- `IErrorHandler.HandleError(Exception)` и `ModalErrorHandler`: показывает `Shell.Current.DisplayAlertAsync("Error", ex.Message, "OK")`, очередь через `SemaphoreSlim(1,1)`, чтобы окна не накладывались. `DisplayAlertAsync` — метод .NET 10 (прежний `DisplayAlert` устарел).
- `TaskUtilities.FireAndForgetSafeAsync(this Task, IErrorHandler?)`: `async void`-обёртка, ловит исключения и отдаёт обработчику.
- `ProjectExtensions.IsNullOrNew([NotNullWhen(false)] this Project?)`: проверка `null` или `ID==0` с аннотацией nullable-потока.
- Брать: интерфейс обработчика ошибок, сериализация показа ошибок, `FireAndForgetSafeAsync`, `NotNullWhen` в расширениях. Не переносить: показ `ex.Message` пользователю как есть (для провайдеров нужен разбор: неверный ключ, лимит, модерация и так далее по F9.1; сообщение может содержать чувствительные данные).

## PageModels: `PageModels/`
- Общий каркас: `partial class X : ObservableObject`, `[ObservableProperty] public partial T Name { get; set; }` (partial-свойства, а не поля), `[RelayCommand]` на `private` методах, из них генерируются `NameCommand`.
- `MainPageModel`: панель с диаграммой категорий, списком проектов и задач. Состояния `IsBusy`, `IsRefreshing`; `Appearing` загружает данные первый раз и сидирует БД (признак `Preferences.Default "is_seeded"`), далее перезагружает при возврате на страницу (флаги `_dataLoaded`, `_isNavigatedTo`); `NavigatedTo/From` выставляют флаг. `LoadData` с `try/finally` гасит `IsBusy` и поднимает `OnPropertyChanged(nameof(HasCompletedTasks))`. Навигация `Shell.Current.GoToAsync("project?id=...")`.
- `ProjectListPageModel`: список проектов, `Appearing`, переход к проекту, добавление.
- `ProjectDetailPageModel : IQueryAttributable, IProjectTaskPageModel`: `ApplyQueryAttributes` различает `id` (загрузка), `refresh` (обновление задач), иначе новый проект; `LoadData` с `IsBusy`; `[NotifyCanExecuteChangedFor(nameof(DeleteCommand))]` на `CanDelete`; `[RelayCommand(CanExecute = nameof(CanDelete))]`; `partial void OnIsCategoryPickerExpandedChanged` вызывает `SemanticScreenReader.Announce`; передача объекта в следующую страницу через `ShellNavigationQueryParameters`; мультивыбор тегов (`SelectedTags` как `IList<object>`).
- `TaskDetailPageModel : IQueryAttributable`: константа ключа `ProjectQueryKey`, `LoadTaskAsync(query)` запускается через `FireAndForgetSafeAsync`; сохранение возвращается `GoToAsync("..?refresh=true")`.
- `ManageMetaPageModel`: редактирование категорий и тегов через `ObservableCollection`, тосты и `SemanticScreenReader.Announce` после действий; `Reset` сбрасывает сидирование.
- `IProjectTaskPageModel`: интерфейс с `NavigateToTaskCommand` и `IsBusy`, нужен, чтобы общий контрол `TaskView` привязывался к любому PageModel через `RelativeSource AncestorType`.
- Брать: partial-свойства, `IQueryAttributable`, `ShellNavigationQueryParameters`, `NotifyCanExecuteChangedFor`, общий интерфейс для контролов, `IsBusy` + `try/finally`, озвучивание изменений. Не переносить: прямые вызовы `Shell.Current` и `AppShell.DisplayToastAsync` из PageModel без абстракции (мешает тестам; вынести в `INavigationService`/`IToastService`), `FireAndForget` на сохранение до навигации (ошибка сохранения теряется после ухода со страницы), хранение флага сидирования в `Preferences`, `new(List)` для принудительного обновления привязки (допустимо, но лучше `ObservableCollection`).

## Pages и контролы: `Pages/`, `Pages/Controls/`
- Страницы: `MainPage`, `ProjectListPage`, `ProjectDetailPage`, `TaskDetailPage`, `ManageMetaPage`. Code-behind минимален: конструктор принимает PageModel, `InitializeComponent(); BindingContext = model;`.
- XAML: `x:DataType` на странице и в `DataTemplate`; `toolkit:EventToCommandBehavior` связывает события страницы `Appearing/NavigatedTo/NavigatedFrom` с командами PageModel (`BindingContext` берётся через `x:Reference`); `SemanticProperties.Description/Hint/HeadingLevel`; `OnIdiom`, `OnPlatform` для размеров и отступов; `BindableLayout` для коротких списков, `CollectionView` для прокручиваемых; `SelectionChangedCommand` + параметр для навигации; `VisualStateManager` для выбранных элементов; `AppThemeBinding` для светлой и тёмной темы; `RelativeSource AncestorType` с явным `x:DataType` для обращения к команде PageModel из шаблона; `TextValidationBehavior` (regex цвета HEX) с `InvalidStyle`; кнопка добавления поверх контента (`AddButton`).
- Контролы: `AddButton` (кнопка со стилем), `CategoryChart` (Syncfusion `SfCircularChart` + `SfShimmer`), `ProjectCardView`, `TagView`, `TaskView` (имеет `BindableProperty TaskCompletedCommand` и обработчик `CheckedChanged`), `ChipDataTemplateSelector` (выбор шаблона по `IsSelected`), `ChartDataLabelConverter` и `ChartDataSemanticConverter` (`IValueConverter` для подписей и текста скринридера), `LegendExt` (наследник `ChartLegend`).
- Shimmer-загрузка: `SfShimmer` с `IsActive` привязанным к `IsBusy` PageModel, пока данные грузятся; `CustomView` рисует заглушку.
- Брать: `x:DataType` везде, `EventToCommandBehavior` вместо code-behind, `BindableProperty` для команд контрола, `DataTemplateSelector`, заглушки загрузки (в спецификации карточки результата тоже занимают место сразу), текстовый вариант для скринридера у графиков. Не переносить: зависимость от Syncfusion без решения владельца; `ContentPage` с жёстко заданными английскими строками (спецификация требует русский и английский, нужна локализация через `.resx`); дубли ключей стилей между `Styles.xaml` и `AppStyles.xaml` (например, `Label` определён дважды, `Border` дважды).

## Ресурсы и стили: `Resources/`
- `Styles/Colors.xaml`: палитра (Primary `#512BD4` и др., оттенки серого), `SolidColorBrush` для каждого цвета.
- `Styles/Styles.xaml`: стандартные стили элементов (Button, Entry, Label, Picker, Switch и так далее) с `VisualStateGroup` для Disabled и `AppThemeBinding` для тем; стили `Page` и `Shell`; стиль `shimmer:SfShimmer`.
- `Styles/AppStyles.xaml`: подключён последним и перекрывает `Styles.xaml`: шкала размеров `size20…size560`, `OnIdiom`-ресурсы `IconSize`, `LayoutPadding`, `LayoutSpacing`, `FontImageSource`-иконки из `FluentUI`, типографика Fluent (`Title1..3`, `Body1/2`, `Caption1/2`, `LargeTitle`, `Display`), стили `Border`/`CardStyle`, `ShimmerCustomViewStyle`.
- `App.xaml` подключает три словаря в порядке Colors, Styles, AppStyles (порядок важен: поздние перекрывают ранние).
- `Fonts/`: OpenSans (Regular, Semibold), SegoeUI-Semibold, FluentSystemIcons-Regular.ttf и `FluentUI.cs` (сгенерированный класс с константами глифов `FluentUI.FontFamily` и именами вида `delete_32_regular`; помечен `internal`). Иконки рисуются через `FontImageSource`, а не картинки.
- `Raw/AboutAssets.txt` (подсказка шаблона), `Raw/SeedData.json`, `AppIcon/*.svg`, `Splash/splash.svg` (логотип .NET).
- Брать: порядок словарей, ресурсы `OnIdiom` для размеров, иконочный шрифт с константами, темизацию через `AppThemeBinding`, шкалу типографики. Не переносить: фирменные цвета и логотип шаблона (заменить), сгенерированный `FluentUI.cs` на 500+ КБ как есть (оставить только нужные глифы).

## Платформы: `Platforms/`
- Android: `MainActivity` (`[Activity(... MainLauncher=true, LaunchMode.SingleTop, ConfigurationChanges=...)]`), `MainApplication` (`CreateMauiApp => MauiProgram.CreateMauiApp()`), `AndroidManifest.xml` с разрешениями `ACCESS_NETWORK_STATE` и `INTERNET`, `Resources/values/colors.xml`. Сохранения в галерею нет: потребуется отдельная работа (MediaStore; на Android 9 и ниже ещё разрешение записи).
- iOS и Mac Catalyst: `AppDelegate : MauiUIApplicationDelegate`, `SceneDelegate : MauiUISceneDelegate`, `Program.Main`, `Info.plist` (ориентации, сцена `__MAUI_DEFAULT_SCENE_CONFIGURATION__`), Mac Catalyst `Entitlements.plist` (песочница и `network.client`), iOS `PrivacyInfo.xcprivacy` (обязательные категории API; запись для `Preferences`, `UserDefaults`, закомментирована; нужна, если использовать `Preferences`).
- Windows: `App.xaml`/`App.xaml.cs` (`MauiWinUIApplication`, пространство `ImageGen.WinUI`), `Package.appxmanifest` (placeholder-идентификатор, `runFullTrust`), `app.manifest` (DPI PerMonitorV2, `longPathAware`).
- Все точки входа делегируют в `MauiProgram.CreateMauiApp()`.
- Брать: единую точку создания приложения. Не переносить: placeholder-идентификаторы. Для сохранения в галерею Android, Photos на iOS нужны новые записи в манифестах (см. `docs/knowledge/maui.md`).

## Итог: что в примере ценно для ImageGen
- Рабочий каркас MAUI: DI, Shell, MVVM Toolkit, темы, адаптивность, доступность, тосты и ошибки.
- Образец загрузки данных на странице (`Appearing` + `IsBusy` + shimmer) для ленты результатов и каталога моделей.
- Паттерн «главная страница + детальные страницы с query-параметрами» для Студии, Галереи, Моделей.
- Чего нет и придётся делать с нуля: HTTP-клиенты провайдеров, шифрованное хранилище секретов, фоновые параллельные задания, сохранение в галерею, локализация, тесты, Web-оболочка.
