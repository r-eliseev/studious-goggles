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

## Разметка и списки
- `Grid` (основной), `VerticalStackLayout`/`HorizontalStackLayout`, `FlexLayout` (перенос чипов), `AbsoluteLayout`, `ScrollView`.
- Для коротких динамических списков `BindableLayout`; для длинных `CollectionView` с `LinearItemsLayout` или `GridItemsLayout` (сетка результатов: `GridItemsLayout` с `Span`, на узком экране две колонки, на широком больше; подгонять через `OnIdiom` или размер окна).
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

## Сохранение изображения в галерею (из заметок: Android)
- Android 10 (API 29) и выше: вставка через `MediaStore.Images.Media` с `ContentValues` (`DisplayName`, `MimeType`, `RelativePath = "Pictures/ImageGen"`, `IsPending`) и запись в `ContentResolver.OpenOutputStream` — разрешение записи не нужно. Android 9 и ниже: нужно разрешение `WRITE_EXTERNAL_STORAGE` и запись в публичную папку с запросом `MediaScanner` (проверить). Android 13 и выше для чтения чужих фото — `READ_MEDIA_IMAGES`; для сохранения собственных не требуется.
- Код в `Platforms/Android` за интерфейсом ядра (например `IGallerySaver`), на других платформах свои реализации: iOS — `PHPhotoLibrary` (+ ключ `NSPhotoLibraryAddUsageDescription` в `Info.plist`), Mac Catalyst — `NSSavePanel`/Photos, Windows — `FileSavePicker` или копирование в `Pictures`.
- Рядом хранится копия в собственном репозитории результатов, файл в галерее — экспорт.

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

## Что MAUI не даёт для этого проекта
- Linux, Docker и веб: поэтому Web API + Vue вынесены в отдельную оболочку (`docs/architecture.md`, ADR 0001).
- Шифрованная БД и фоновые сервисы: нужны отдельные решения.
