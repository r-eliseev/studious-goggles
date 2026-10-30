# Справочник: .NET MAUI для этого проекта

Написан по знаниям модели, не по сверке с живой документацией в этой сессии. Пункты, помеченные «проверить», перед использованием сверьте с документацией. Версия в примере: .NET 10, `$(MauiVersion)` из SDK, `CommunityToolkit.Maui 12.3.0`, `CommunityToolkit.Mvvm 8.4.2`.

Источники для сверки: https://learn.microsoft.com/dotnet/maui/ , https://learn.microsoft.com/dotnet/communitytoolkit/maui/ , https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/

## Платформы
- Поддерживаются: Android, iOS, Mac Catalyst (macOS), Windows (WinUI 3). Linux в MAUI официально нет; в `ImageGen.csproj` примера такой цели тоже нет. Linux закрывается другим способом (см. `docs/status.md`).
- Сборка iOS и Mac Catalyst требует macOS с Xcode; на Windows они в `TargetFrameworks` примера пропускаются только на Linux, поэтому на Windows без Mac-хоста их сборка может не пройти (проверить; для работы на Windows собирайте `-f net10.0-windows10.0.19041.0` или `-f net10.0-android`).
- Нагрузки: `dotnet workload install maui` (проверить актуальность команды для .NET 10); `dotnet workload list`.

## Структура single-project
- Один `.csproj` на все платформы, `SingleProject=true`. Платформенный код в `Platforms/<Android|iOS|MacCatalyst|Windows>/` собирается только для своей платформы автоматически.
- Условная компиляция: `#if ANDROID`, `#if IOS`, `#if MACCATALYST`, `#if WINDOWS`; для проверки на лету — `OperatingSystem.IsAndroid()`, `DeviceInfo.Platform`.
- Ресурсы: `Resources/AppIcon`, `Splash`, `Images`, `Fonts`, `Raw`, `Styles`; `MauiImage` масштабируется Resizetizer (SVG и PNG), имена только в нижнем регистре, цифры и подчёркивание.
- Все платформенные точки входа вызывают `MauiProgram.CreateMauiApp()`.

## Приложение и жизненный цикл
- `App.CreateWindow(IActivationState?)` возвращает `Window` со стартовой страницей или `Shell` (`MainPage` у `Application` в новых проектах не используется).
- События страницы: `Appearing`, `Disappearing`, `NavigatedTo`, `NavigatedFrom`; события окна: `Created`, `Activated`, `Deactivated`, `Stopped`, `Resumed`, `Destroying` (для сохранения состояния и остановки подписок).
- Под Android и iOS приложение может быть остановлено системой в фоне: долгие генерации нельзя держать только в памяти UI; результат писать сразу на диск, статус задания хранить в БД (требование F7.2). Фоновые сервисы платформ (Android foreground service, iOS background tasks) — отдельная работа, не входит в MAUI из коробки (проверить).

## XAML и привязки
- `x:DataType` обязателен для компилируемых привязок; в `DataTemplate` и при `Source={RelativeSource AncestorType=...}` тип указывать явно (см. примеры в `Pages/`).
- Ресурсы: `ResourceDictionary`, `StaticResource` (разрешается при загрузке), `DynamicResource` (следит за изменением); поздние словари перекрывают ранние.
- `AppThemeBinding Light=..., Dark=...` для темы; смена: `Application.Current.UserAppTheme = AppTheme.Dark/Light/Unspecified`.
- `OnPlatform` и `OnIdiom` в XAML для различий платформ и форм-факторов (`Phone`, `Tablet`, `Desktop`, `Watch`, `TV`).
- Конвертеры `IValueConverter`; в CommunityToolkit.Maui готовые (`InvertedBoolConverter` и другие).
- Behaviors: `EventToCommandBehavior`, `TextValidationBehavior` и т. д. из CommunityToolkit.Maui; подключение `UseMauiCommunityToolkit()` в `MauiProgram`.
- Стили по умолчанию на `TargetType`, именованные по `x:Key`; `Style TargetType="Page" ApplyToDerivedTypes="True"` для общего фона.
- Не вкладывать `CollectionView` в `ScrollView` без причины (теряется виртуализация); в примере так сделано для коротких списков.

- **Клавиатура Android в `Editor` (задача 050, собрано, на устройстве не проверено):** действие «Отправить» — `EditorHandler.Mapper.AppendToMapping`, `PlatformView.ImeOptions = ImeAction.Send`, `EditorAction` в `Platforms/Android/ChatEditorImeHandling.cs`. Чтобы клавиатура показала «Отправить», поле переводится в однострочный ввод (`SetRawInputType` без `TextFlagMultiLine`): в однострочном поле Android прокручивает длинный текст по горизонтали, а не переносит его. Это проверяет задача 016 (пункт 11).
- **Клавиши в `Editor` на Windows (проверено 2026-10-28, задача 056):** у `Editor` нет событий клавиш. Перехват — маппинг `EditorHandler.Mapper.AppendToMapping` и `PreviewKeyDown` у нативного WinUI `TextBox`: обычный `KeyDown` приходит уже после вставки перевода строки. Shift — `InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)`. Так сделано поле чата `ChatEditor` (Enter — отправить, Shift+Enter — новая строка).
- **Клавиатура Android и safe area (.NET 10, задача 095):** у `ContentPage` по умолчанию `SafeAreaEdges=None`, у layout — `Container`; клавиатура не входит в safe area layout. На семи корневых страницах стоит `SafeAreaEdges="All"` атрибутом, чтобы экранный ввод учитывался без дополнительного неявного стиля `ContentPage`, который перекрыл бы общий стиль `Page`. По [документации safe area](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/safe-area?view=net-maui-10.0) для Android при необходимости `WindowSoftInputModeAdjust(Resize)` применяется вместе с `All`; [руководство по soft keyboard](https://learn.microsoft.com/en-us/dotnet/maui/android/platform-specifics/soft-keyboard-input-mode). Android target SDK текущей сборки — 36. Сборка проверена, на устройстве не проверено; `Resize` не включали.

## Разметка и списки
- `Grid` (основной), `VerticalStackLayout`/`HorizontalStackLayout`, `FlexLayout` (перенос чипов), `AbsoluteLayout`, `ScrollView`.
- Для коротких динамических списков `BindableLayout`; для длинных `CollectionView` с `LinearItemsLayout` или `GridItemsLayout` (сетка результатов: `GridItemsLayout` с `Span`, на узком экране две колонки, на широком больше; подгонять через `OnIdiom` или размер окна).
- **Прокрутка ленты чата (задача 097):** `CollectionView` с именем `Transcript` прокручивается через `ScrollTo(индекс, ScrollToPosition.End, animate: false)` внутри `Dispatcher.Dispatch` после изменения `Items`. Решение следует ли за концом ведёт `TranscriptFollower`: изменения строк всегда возобновляют follow, обновление содержимого карточки и размера — только пока follow включён. Направление пользовательской прокрутки берётся из `Scrolled.VerticalDelta` после фильтрации программной прокрутки, pending scroll и событий без вертикального движения; последний видимый индекс при движении вниз — сигнал возобновить follow, после чего лента позиционируется в конец. Корневые строки сообщений и прогонов сообщают об изменении размера. Android-сборка проверена, на устройстве не проверено.
- `SelectionMode`, `SelectionChangedCommand`; после обработки выбор сбрасывать, иначе повторное нажатие не сработает (в примере этого нет — известная особенность).
- `RefreshView` — стандартное pull-to-refresh MAUI; в примере используется Syncfusion `SfPullToRefresh`.

## Shell и навигация
- `Shell` с `ShellContent`/`TabBar`/`FlyoutItem`; `Route` для верхнего уровня, `Routing.RegisterRoute` или `AddTransientWithShellRoute` для дочерних страниц.
- `Shell.Current.GoToAsync("route?id=1")` для строки параметров, `GoToAsync("route", new ShellNavigationQueryParameters { {"key", obj} })` для объектов (одноразовая передача), `".."` — назад, `"//main"` — абсолютный маршрут.
- Приёмник: `IQueryAttributable.ApplyQueryAttributes(IDictionary<string, object>)` или `[QueryProperty]`.
- Для спецификации нужна нижняя навигация на узком экране и боковая на широком: `TabBar` для телефона, `Flyout` для десктопа; выбор через `OnIdiom` или раздельные оболочки (проверить, как лучше).

## MVVM (CommunityToolkit.Mvvm)
- `ObservableObject`, `[ObservableProperty]`, `[RelayCommand]` (асинхронные команды `AsyncRelayCommand`, отмена `IncludeCancelCommand = true`), `[NotifyCanExecuteChangedFor]`, `[NotifyPropertyChangedFor]`, `partial void OnXChanged(T value)`.
- Асинхронная команда блокирует повторный вызов, пока выполняется, если `AllowConcurrentExecutions` не включён; это удобно для кнопки «Запустить».
- `WeakReferenceMessenger` для сообщений между PageModel без прямых ссылок (например, «результат готов» → Галерея).

## Платформенные API (MAUI Essentials)
- `Preferences` — несекретные настройки; не шифруется. Не для ключей.
- `SecureStorage` — секреты (Keychain/Keystore/DPAPI); асинхронный `GetAsync/SetAsync/Remove`. Особенности: на Android данные привязаны к приложению, при автобэкапе и переустановке возможна потеря ключа шифрования (для приложений с `allowBackup="true"`, как в манифесте примера, нужны правила исключения; проверить по документации); на iOS и Mac нужны правильные Keychain-права для симулятора и распространения; в unpackaged Windows работает через DPAPI. Любой вызов оборачивать в `try/catch`: на некоторых устройствах он бросает исключение при недоступном хранилище.
- `FileSystem.AppDataDirectory`, `CacheDirectory`, `OpenAppPackageFileAsync` (для `Resources/Raw`).
- `FilePicker`, `MediaPicker` (референсные картинки: выбрать из галереи, снять, вставка из буфера), `Share`, `Clipboard`, `Launcher`, `Connectivity`, `DeviceInfo`, `DeviceDisplay`, `MainThread`, `SemanticScreenReader`, `Vibration`/`Haptic`.
- Разрешения (`Permissions.RequestAsync<...>`) и их записи в манифестах нужны для камеры, чтения и записи фото.

- **Картинка из файла на Windows (проверено 2026-10-28, задача 054):** `ImageSource.FromFile` с абсолютным путём, в котором есть `/` (`C:\…\Data\images/…/x.jpg`), картинку молча не показывает; тот же путь с `\` — показывает (проверено запуском у владельца). WinRT `StorageFile.GetFileFromPathAsync` ведёт себя так же: с `/` — 0x80070002 «файл не найден», с `\` — открывает (проверено в PowerShell). Использует ли MAUI именно этот вызов — не проверялось по исходникам. Вывод: путь к файлу для оболочки собирать с разделителями ОС (`FileImageStore.GetFullPath`), даже если в БД он хранится с `/`.

## Сохранение изображения в галерею (из заметок: Android)
- Android 10 (API 29) и выше: вставка через `MediaStore.Images.Media` с `ContentValues` (`DisplayName`, `MimeType`, `RelativePath = "Pictures/ImageGen"`, `IsPending`) и запись в `ContentResolver.OpenOutputStream` — разрешение записи не нужно. Android 9 и ниже: нужно разрешение `WRITE_EXTERNAL_STORAGE` и запись в публичную папку с запросом `MediaScanner` (проверить). Android 13 и выше для чтения чужих фото — `READ_MEDIA_IMAGES`; для сохранения собственных не требуется.
- Код в `Platforms/Android` за интерфейсом ядра (например `IGallerySaver`), на других платформах свои реализации: iOS — `PHPhotoLibrary` (+ ключ `NSPhotoLibraryAddUsageDescription` в `Info.plist`), Mac Catalyst — `NSSavePanel`/Photos, Windows — `FileSavePicker` или копирование в `Pictures`.
- Рядом хранится копия в собственном репозитории результатов, файл в галерее — экспорт.

## Бэкап и перенос данных на Android (задача 115)
- Приложение не участвует в автоматическом бэкапе: в `Platforms/Android/AndroidManifest.xml` стоит `android:allowBackup="false"`, плюс `android:fullBackupContent="@xml/backup_rules"` и `android:dataExtractionRules="@xml/data_extraction_rules"`.
- Явные правила deny-all — defense in depth: документация Android не обещает, что инструмент переноса OEM учитывает один только флаг манифеста. `backup_rules.xml` (Android 11 и ниже) и `data_extraction_rules.xml` (Android 12+, секции `cloud-backup` и `device-transfer`) исключают все девять доменов схемы с `path="."`: `root`, `file`, `database`, `sharedpref`, `external`, `device_root`, `device_file`, `device_database`, `device_sharedpref`. Ни одного `include`.
- Проверено: `ImageGen.Presentation.Tests/Design/AndroidBackupPolicyTests.cs` проверяет атрибуты манифеста, отсутствие `include` и точный набор доменов в обеих секциях; сборка Android и разбор итогового манифеста из APK (`aapt2 dump xmltree`) подтверждают, что правила попали в пакет.
- **Не проверено на устройстве:** поведение переноса данных (D2D) на реальном телефоне. Source-тесты подтверждают конфигурацию, но не поведение OEM-инструмента.
- Свой экспорт пользовательских данных (F7.6) ещё не реализован: приложение не умеет отдавать зашифрованную копию, поэтому и включать её в правила нечего.

## Темизация, шрифты, иконки
- Иконки через шрифт и `FontImageSource` (как в примере) или SVG через `MauiImage`.
- Шрифты в `Resources/Fonts`, регистрация в `ConfigureFonts` с псевдонимом.
- Цвета платформенных частей (Android `colors.xml`) дублируют палитру, не синхронизируются автоматически.

## Локализация
- Ресурсы `.resx` + `CultureInfo`; в XAML через расширение разметки или обёртку над `ResourceManager`. Нужны русский и английский (F10.1). Строки в примере зашиты на английском.

## Доступность
- `SemanticProperties.Description`, `Hint`, `HeadingLevel`; `AutomationProperties.IsInAccessibleTree`; минимальная цель касания 44; `SemanticScreenReader.Announce` для динамических сообщений; клавиатурная навигация на Windows (в примере обработчики `SingleSelectionFollowsFocus=false`, `IsTabStop`).

## Производительность
- Включать компилируемые привязки и XAML SourceGen (в примере включены).
- Крупные изображения: уменьшать превью (миниатюры в сетке), не грузить оригиналы; `Image.Source` из файла/потока, `ImageSource.FromFile`/`FromStream`; освобождать большие потоки.
- Не создавать тысячи `View` в `BindableLayout`: для длинных лент `CollectionView`.

## Отладка и запуск
- Windows: `dotnet build -f net10.0-windows10.0.19041.0`, запуск из Visual Studio или `dotnet run -f net10.0-windows10.0.19041.0`; Android: эмулятор и `dotnet build -t:Run -f net10.0-android` (проверить); iOS и Mac Catalyst только с Mac.
- Hot Reload для XAML работает в Visual Studio/VS Code, в Debug.
- Журнал: `builder.Logging.AddDebug()` (как в примере, только в `DEBUG`).

## Известные особенности из примера
- Toast из CommunityToolkit.Maui на Windows не работает (в примере пропускается) — использовать Snackbar или собственное уведомление.
- `Shell.Current.DisplayAlertAsync` — .NET 10 API; в более ранних версиях был `DisplayAlert`.
- Windows: `CollectionView` по умолчанию выбирает элемент при фокусе; для клавиатурной доступности это отключено в `MauiProgram`.

## Проверено пробной сборкой (2026-10-27, SDK 10.0.402, MAUI 10.0.110)
Проверено в отдельной папке вне репозитория: шаблон `dotnet new maui` + центральные версии пакетов + сборка под Windows и Android + запуск под Windows.
- Шаблон `dotnet new maui` создаёт пустое приложение (`MainPage` со счётчиком, `dotnet_bot.png`), `ApplicationId` `com.companyname.<имя>` и `$(MauiVersion)` в `PackageReference`. При `ManagePackageVersionsCentrally=true` атрибут `Version` в `PackageReference` запрещён: версии `Microsoft.Maui.Controls` и `Microsoft.Extensions.Logging.Debug` переносятся в `Directory.Packages.props`.
- Файлы шаблона отформатированы табами: `dotnet format --verify-no-changes` падает, пока не выполнить один раз `dotnet format` без `--verify-no-changes` (при `.editorconfig` с `indent_style = space`).
- С пакетом `CommunityToolkit.Maui` сборка падает с ошибкой `MCT001`, если в `MauiProgram` нет `.UseMauiCommunityToolkit()` сразу после `.UseMauiApp<App>()` (и `using CommunityToolkit.Maui;`).
- Длинный путь к папке проекта ломает Windows-сборку: ошибка `MSB3030` про `WindowsAppSdk.AppxDeploymentExtensions.Desktop-EventLog-Instrumentation.dll` в `MsixContent`. Путь `C:\git\studious-goggles` подходит; клон в глубокую папку (например во временную) нет.
- Строго типизированный класс из `.resx` без Visual Studio под Windows-целью не генерируется (под Android генерируется). Рабочий способ: `.resx` + вручную написанный класс доступа на `ResourceManager` и явный `LogicalName` у `EmbeddedResource` (имя ресурса проверено в собранной сборке).
- `SecureStorage.Default.SetAsync/GetAsync` работает в неупакованном Windows-приложении (`WindowsPackageType=None`). `FileSystem.AppDataDirectory` там равен `%LOCALAPPDATA%\<издатель>\<ApplicationId>\Data` (у шаблона издатель `User Name`).
- `dotnet build <slnx>` на Windows собирает MAUI-проект под все цели, включая iOS и Mac Catalyst; это долго. Для проверки ядра собирать и тестировать отдельные проекты.

## Что MAUI не даёт для этого проекта
- Linux, Docker и веб: поэтому Web API + Vue вынесены в отдельную оболочку (`docs/architecture.md`, ADR 0001).
- Шифрованная БД и фоновые сервисы: нужны отдельные решения.
- **Темы MAUI без ThemeDictionaries (задача 062, собрано на Android):** `ResourceDictionary` с `ThemeDictionaries` в MAUI нет, поэтому шесть словарей (`Resources/Themes/<Стиль><Тема>.xaml` с code-behind) подменяются в `Application.Resources.MergedDictionaries` (`Services/MauiAppearanceApplier.cs`). Тема приложения — `Application.UserAppTheme`; системная — `Application.PlatformAppTheme` (не `RequestedTheme`: он показывает уже выбранную явно тему). Значения по платформе — `OnPlatform` (`WinUI`, `MacCatalyst`, `iOS`, `Android`); выравнивание и стиль шрифта — `x:Static` (`LayoutOptions.End`, `FontAttributes.Italic`).
- **Копирование текста в MAUI (задача 055):** `Label` не выделяется, поэтому у ответа ассистента и у промпта прогона есть кнопка «Copy» (`Command="{Binding Copy.CopyCommand}"`), а «Copied» показывается через `CopyTextAction.IsCopied`. Долгое нажатие на сообщение не сделано. В Avalonia текст выделяется `SelectableTextBlock`.
- **Стили MAUI на ресурсах Ig* (задача 064, собрано на Android):** `Resources/Styles/Styles.xaml` без ключей шаблона (`Colors.xaml` удалён). `IgControlHeight` — `OnPlatform` в `Themes/Common.xaml`, в стиле берётся через `StaticResource` (Common объединён первым в `App.xaml`): `DynamicResource` на `OnPlatform` не проверен. `Button.CornerRadius` в MAUI — `int`, поэтому в словарях темы есть `IgButtonCornerRadius` (`x:Int32`). Отдельный размер шрифта на iOS и Android (17 и 16) не применяется: `IgFontSizeBody` — обычное число (десктопное значение стиля). Классы `.primary`, `.ghost`, … — неявные стили (`Class="..."`); состояния кнопки — `VisualStateManager` в базовом стиле `Button`.
- **Экран моделей сессии (задача 090):** страница и панель чата используют общий `Controls/SessionModelsView`. У `RadioButton` нет `Command`, поэтому выбор модели ассистента идёт через `CheckedChanged` в code-behind этого control; группа задаётся `RadioButtonGroup.GroupName` на контейнере списка. Порядок кнопок Save/Cancel задаётся там же через `Grid.SetColumn`: на Mac Catalyst Cancel слева. Вкладки и общие кнопки разделяют выбор изображения и ассистента; проверить клавиатурную навигацию и диктор вручную.

## Меню и ускорители (задача 073)
- Строка меню — `ContentPage.MenuBarItems` (`MenuBarItem` → `MenuFlyoutItem`). Ускоритель — `KeyboardAccelerator` в `MenuFlyoutItem.KeyboardAccelerators`; модификатор через `Modifiers="{OnPlatform WinUI=Ctrl, MacCatalyst=Cmd}"` (собирается на Android, компилятор XAML принимает).
- Буквенные ускорители без модификатора не ставим: они перехватывают ввод в поле чата (поэтому R, E и Ctrl+C без ускорителя).
- Выбранная карточка — `ChatPageModel.SelectedCard`: тап по карточке (`TapGestureRecognizer` с `x:Reference Page`) вызывает `SelectCardCommand`; пункты меню «Card» привязаны к `SelectedCard.<Команда>`.
- **Не проверено:** поведение строки меню и ускорителей под Windows и Mac Catalyst (нужны Windows и macOS, пункт в задаче 016); на телефонах `MenuBarItems` не показываются.

## Публикация MAUI под Windows с RID
- Для многоцелевого MAUI-проекта `dotnet publish -f net10.0-windows10.0.19041.0 -r <rid>` задаёт RID при restore всех target frameworks; при restore Android может завершиться `NU1102` для `Mono.win-x64`. `-p:TargetFrameworks=net10.0-windows10.0.19041.0` сужает restore приложения, но глобальное MSBuild-свойство попадает и в project references: в запуске 10 это привело к `NETSDK1005` — Core, Data и Presentation ожидали отсутствующий `net10.0`.
- `-p:RuntimeIdentifierOverride=<rid>` не задаёт SDK RID: в релизе `v0.1.4` (проверено 2026-10-29) пакет `win-arm64` содержал x64 PE-файлы.
- Рабочий способ проверен 2026-10-30 в GitHub Actions `release.yml`, запуск 11 (`441c03ac3cbb9cc0525e8d51f1da5093e1add720`): передать `-p:ImageGenWindowsRid=<rid>`, а в `ImageGen.Maui.csproj` установить `RuntimeIdentifier` только когда target platform — Windows и свойство задано. Так project references не получают RID и сохраняют свои TFM.
- Шаг `Check PE machine` проверяет `ImageGen.Maui.exe`, `coreclr.dll` и `Microsoft.WindowsAppRuntime.Bootstrap.dll` до упаковки. Артефакты запуска 11 дополнительно проверены через `file`: все пять проверенных PE-файлов x64 пакета (`ImageGen.Maui.exe`, `coreclr.dll`, `hostfxr.dll`, `Microsoft.WindowsAppRuntime.Bootstrap.dll`, `Microsoft.ui.xaml.dll`) — x86-64; в arm64 пакете те же файлы — AArch64. Запуск пакета на Windows on ARM не проверен.

