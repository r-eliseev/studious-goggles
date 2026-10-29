---
id: 094
status: approved
assignee:
claimed:
created: 2026-10-29
---

# Avalonia на Android и iOS вместо MAUI: исследование

## Описание
Просьба владельца (2026-10-29): проверить, может ли Avalonia-оболочка обслуживать телефоны (Android, iOS), чтобы убрать MAUI-оболочку и размечать каждый экран один раз. Повод — замечания после проверки MAUI (задачи 088–093): разметка экранов дублируется между оболочками (в MAUI экран моделей ещё и скопирован в боковую панель `ChatPage.xaml`), поведение расходится (кнопка «…» карточки работает в Avalonia и не работает в MAUI).

ADR 0006 рассматривал вариант «Avalonia везде, без MAUI» и отложил его: «мобильная Avalonia требует отдельной проверки зрелости». Эта задача и есть такая проверка. Источник: https://avaloniaui.net/avalonia/mobile (страница производителя). Там заявлены Android и iOS из того же проекта и той же разметки, отрисовка через Skia, .NET 10, доступность через `AutomationProperties`, сборка и подпись обычной .NET-цепочкой. Сами же авторы пишут, что мобильные цели у Avalonia самые молодые и сторонних контролов для телефонов мало. Ничего из этого на нашем коде не проверено.

Что MAUI-оболочка сейчас берёт у платформы (`ImageGen.Maui/Services`, `Platforms/`), и у Avalonia на телефоне готового аналога может не быть:
- хранилище ключей: `MauiSecretStore`/`MauiSecretPersistence` (`SecureStorage`: Android Keystore, iOS Keychain);
- сохранение в галерею: `MauiImageSaver` (`MediaStore` на Android, `PHPhotoLibrary` на iOS, задача 080);
- выбор файлов и буфер обмена: `MauiFilePickerService`, `MauiClipboardService` (у Avalonia есть `IStorageProvider` и буфер обмена — работают ли на телефоне, проверить);
- отправка по Enter с экранной клавиатуры Android: `Platforms/Android/ChatEditorImeHandling.cs` (задача 050);
- раскладка телефона: узкий экран, нижнее меню карточки, долгое нажатие, свайп в списке сессий, системная кнопка «назад».

Результат — документ и решение владельца, а не перенос. Если переход одобрят, его делают отдельными задачами.

Зависит от: —
Среда: Linux или Windows с Android SDK (пробная сборка под Android; эмулятор или устройство — неизвестно, есть ли). iOS и Mac Catalyst — только по документации, с пометкой «не проверено»: macOS-машины нет.

## Критерии готовности
- Пробный проект в `poc/backlog/094-avalonia-mobile-research/`: Avalonia-приложение под `net10.0-android`, которое ссылается на `ImageGen.Presentation` и показывает хотя бы список сессий и чат на наших моделях страниц. Записано, собирается ли оно, запускается ли (эмулятор или устройство) и что пришлось обойти.
- По каждой платформенной службе из «Описания» записано: есть ли готовое в Avalonia или пакете (имя, лицензия, живость), сколько своего кода нужно, можно ли перенести код из MAUI-оболочки.
- Оценены доступность (TalkBack, по возможности на устройстве), экранная клавиатура и поле ввода чата, производительность ленты с картинками, размер APK в сравнении с MAUI (около 35 МБ, Release, 2026-10-29).
- Выводы в `docs/knowledge/avalonia-mobile.md`; черновик ADR в `docs/decisions/` со статусом `proposed`, который в случае перехода заменит ADR 0006 в части MAUI. Варианты: остаться на двух оболочках; Avalonia везде; Avalonia на десктопе и Android, MAUI только на iOS. Для каждого — что выигрываем, что теряем, примерный объём переноса.
- Вопросы к владельцу — в «Комментариях» и в `docs/status.md`, «Открытые вопросы».

## Анализ

### Что уже известно (документы и код, 2026-10-29)
- **Версии.** В проекте Avalonia 12.1.4 (`ImageGen/Directory.Packages.props`). Пакеты `Avalonia.Android` и `Avalonia.iOS` той же версии 12.1.4 есть на nuget.org (опубликованы 2026-10-09, лицензия MIT; проверено по nuspec): `Avalonia.Android` — цель `net10.0-android36.0`, зависит от `Avalonia.Skia`, `Avalonia.HarfBuzz`, `Xamarin.AndroidX.AppCompat` 1.7.1.3, `Xamarin.AndroidX.Window`, `Xamarin.AndroidX.DocumentFile`; `Avalonia.iOS` — `net10.0-ios26.0`, `net10.0-maccatalyst26.0`, `net10.0-tvos26.0`. Значит, версию Avalonia поднимать не нужно, а Android требует ту же платформу API 36, что и MAUI.
- **Поддержка платформ** (docs.avaloniaui.net, «Supported platforms», 2026-10-29): Android 16 (API 36) — Tier 1, Android 12–15 — Tier 2, 11 и ниже — Tier 3; iOS 26 — Tier 1, iOS 18 — Tier 2. Минимум .NET 10, мобильная поддержка следует политике MAUI. Сборка Android — обычный workload `android` и Android SDK, на Linux поддерживается (там же гайд «Configure Android debugging on Linux»). iOS по документации «must be built on macOS»; с Windows — только через сетевой Mac-хост. Без Mac iOS не собрать ни Avalonia, ни MAUI (MAUI iOS и Mac Catalyst сейчас и так не публикуются, `docs/knowledge/dev-environment.md`).
- **Что есть в Avalonia для телефона** (по списку API документации, на нашем коде не проверено): `ISingleViewApplicationLifetime` (одно представление вместо окна), `AvaloniaMainActivity`, `AvaloniaAndroidApplication<TApp>`, `AvaloniaAppDelegate<TApp>`; `IInputPane` (состояние экранной клавиатуры) и `IInsetsManager` (безопасные области, edge-to-edge); `TextInputOptions` и `TextInputReturnKeyType` (клавиша «Отправить» на экранной клавиатуре); `AndroidBackRequestedEventArgs` (системная кнопка «назад»); распознаватели `Swipe`, `Pull`, `Pinch`, `Scroll` и `RefreshContainer`; `IClipboard`, `ILauncher`; `IStorageProvider` — по таблице документации `OpenFilePickerAsync` и `SaveFilePickerAsync` есть на Android и iOS, а `TryGetFileFromPathAsync` — нет. Доступность Android — пространство имён `Avalonia.Android.Automation` (`INodeInfoProvider`); насколько TalkBack читает наши `AutomationProperties.Name` — неизвестно.
- **Чего в Avalonia нет:** защищённого хранилища (ADR 0008) и сохранения в галерею — это свой код в оболочке.
- **Avalonia-оболочка сейчас только десктопная:** `ImageGen.Avalonia.csproj` — `WinExe`, `net10.0`, пакеты `Avalonia.Desktop`, `Tmds.DBus`, `System.Security.Cryptography.ProtectedData`; `Program` запускает `StartWithClassicDesktopLifetime`; `MainWindow : Window`. Все платформенные службы получают `Func<Window?>` (`AppServices.MainWindowOwner`): `AvaloniaFilePickerService`, `AvaloniaClipboardService`, `AvaloniaImageSaver`, `AvaloniaFolderLauncher`, `AvaloniaDialogService` (последний создаёт `new Window` для каждого диалога — на телефоне окон нет). `DesktopAppPaths`, `DesktopSecretStore` — десктопные. Навигация (`AvaloniaNavigationService`) держит стек в `ContentControl` (`AttachHost`) и от окна не зависит. Разметка экранов — `UserControl` в `Views/` (Sessions, Chat, ApiKey, ModelPicker, SessionModels, SessionParameters, Settings); 71 упоминание `AutomationProperties`.
- **Платформенные интерфейсы, которые понадобятся на телефоне** (Core: `ISecretStore`, `IAppPaths`; Presentation: `ISecretPersistence`, `IFilePickerService`, `IClipboardService`, `IImageSaver`, `IFolderLauncher`, `IDialogService`, `INavigationService`, `IMainThread`; плюс применение стиля и темы `AvaloniaAppearanceApplier`):

| Служба | MAUI сейчас | Avalonia на телефоне: что ожидаем | Проверить |
|---|---|---|---|
| `ISecretStore`, `ISecretPersistence` | `MauiSecretStore` на `SecureStorage` (Android Keystore, iOS Keychain) | готового нет; варианты: свой ключ AES-GCM в `AndroidKeyStore` + зашифрованный файл в папке данных; пакет AndroidX Security (`EncryptedSharedPreferences`; живость пакета — неизвестно); iOS — Keychain через `Security.SecRecord` (только по документации) | объём кода, сохранность после перезапуска, поведение при `allowBackup` |
| `IAppPaths` | `FileSystem.AppDataDirectory` | `Environment.SpecialFolder.LocalApplicationData` или `Context.FilesDir`; куда указывает первое на Android — неизвестно | путь, логи, БД SQLite открывается |
| `IImageSaver` | `MauiImageSaver`: `MediaStore` (Android 10+), папка Pictures (до API 28), `PHPhotoLibrary` (iOS) | Android-ветку MAUI можно перенести почти дословно, заменив `Platform.AppContext` на контекст активности; iOS — так же по документации | картинка видна в «Галерее» |
| `IFilePickerService` | `FilePicker` | `IStorageProvider.OpenFilePickerAsync` (заявлено для Android) | выбор фото, чтение потока `content://` |
| `IClipboardService` | `Clipboard` | `TopLevel.Clipboard` | копирование промпта |
| `IFolderLauncher` | на телефоне скрыто (`IsSupported=false`) | так же скрыть | — |
| `IDialogService` | `DisplayAlertAsync`, `DisplayPromptAsync` | окна нет: диалог как слой поверх `MainView` | подтверждения, ввод имени |
| `INavigationService` | Shell | тот же `ContentControl`-стек + «назад» системной кнопкой | возврат назад, выход из приложения на корне |
| `IMainThread` | `MainThread` | `Dispatcher.UIThread` (как на десктопе) | — |
| Enter и IME (задача 050) | `ChatEditorImeHandling` (`ImeAction.Send`) | `TextInputOptions.ReturnKeyType=Send`; туннельный `KeyDown` из десктопа | «Отправить» на клавиатуре отправляет |
| Клавиатура закрывает поле (задача 095) | не решено | `IInputPane`, `IInsetsManager` | поле ввода видно над клавиатурой |
| Жизненный цикл | `MauiApplication`, `MainActivity` | `AvaloniaMainActivity`; пауза и возобновление — неизвестно, какие события | генерация идёт после сворачивания (сейчас фоновой службы нет и в MAUI) |

- **Уже проверенное в среде** (`docs/knowledge/dev-environment.md`, `claude-cloud-session.md`, `vps-session.md`): Android-сборка MAUI идёт на Linux (облачная сессия, VPS). Эмулятора нет нигде: нет `/dev/kvm`, x86_64-образ без ускорения не загрузился за 31 минуту. Запуск возможен только на реальном телефоне: с VPS через `adb connect` по сети, или с Windows-машины по USB (Android SDK там «не проверено»). Размер APK MAUI — около 35 МБ (Release, 2026-10-29, из «Описания»; набор ABI и формат не записаны).

### Вопросы, которые снимает исследование
1. Собирается ли Android-голова Avalonia 12.1.4 на Linux рядом с `ImageGen.Presentation` и `ImageGen.Data` (SQLite на Android через `Microsoft.Data.Sqlite` 10.0.12 под Avalonia, без MAUI).
2. Переносятся ли наши `Views/*.axaml` без правок: сколько разметки годится как есть и что ломает узкий экран (боковая панель, меню «…», `ContextMenu` против нижнего меню, долгое нажатие, свайп).
3. Как оболочку разделить для общего кода: стандартная раскладка шаблона `avalonia.xplat` (общая библиотека + головы Desktop, Android, iOS) — так ли она устроена в версии 12; что из `ImageGen.Avalonia` уезжает в головы (`Tmds.DBus`, DPAPI, `MainWindow`, `Program`).
4. Секреты: какой способ на Android (свой `AndroidKeyStore` или пакет), сколько кода, есть ли живой пакет с MIT-лицензией; iOS — только по документации.
5. Клавиатура: `ReturnKeyType`, перевод строки Shift+Enter с аппаратной клавиатуры, сдвиг поля над клавиатурой.
6. Доступность: читает ли TalkBack имена из `AutomationProperties` (только на устройстве).
7. Производительность: прокрутка ленты с картинками, холодный старт, память.
8. Размер APK Release при одинаковых условиях (ABI, обрезка) в сравнении с MAUI.
9. iOS без Mac: по документации невозможно; проверить, ставится ли workload `ios` на Linux хотя бы для компиляции C# (ожидание — нет).
10. Есть ли в мобильной Avalonia платные части (Avalonia Accelerate и подобное), нужные нам, — неизвестно.

### Пересечения с другими задачами
Пробный код лежит в `poc/` и `ImageGen/` не меняет, поэтому с 088–093, 095, 097, 098 по файлам не конфликтует. Если пробная голова подключает разметку из `ImageGen.Avalonia/Views` ссылками, правки этих задач могут её сломать; пробный код не поддерживается, в отчёте фиксируется коммит, на котором проверяли. 095 (клавиатура закрывает поле), 097 (автопрокрутка чата) и 098 (узкая шапка чата) — готовые сценарии сравнения «как это в MAUI и как в Avalonia»; результат 094 их не блокирует (решение владельца 2026-10-29).

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, членов, ключей строк и тестов переноси дословно. Если шаг не получается так, как написано (API другое, компилятор не принимает разметку), не придумывай обход: опиши в «Комментариях», что пробовал, поставь `blocked` и остановись.

Исключение для этой задачи (решение координатора): весь код — пробный, только в `poc/`, поэтому TDD и unit-тесты не применяются. «Сборка и тесты из „Проверки“» здесь означают команды сборки и замеров из подраздела «Проверка». `ImageGen/` не меняется ни в одном шаге. Это исследование: исход «не работает» — тоже результат. Если шаг прямо задаёт ветку на случай неудачи («если не собирается → запиши … и переходи к шагу N»), иди по ветке и `blocked` не ставь. `blocked` ставь, только если ветки нет.

Выполнять после: — (файлы других задач не меняются; разметка `ImageGen/ImageGen.Avalonia/Views` подключается ссылками и не правится).

Решения владельца (2026-10-29), действуют во всех шагах:
- Телефон есть и подключается с VPS через `adb connect` (`docs/knowledge/vps-session.md`, «Отладка Android: реальное устройство по сети»). Если `adb devices` не показывает устройство в состоянии `device`, шаги на устройстве не выполняются. Тогда в «Отчёте» и в `docs/knowledge/avalonia-mobile.md` пишется «не проверено, проверить владельцу», а шаги попадают в «Ручную проверку».
- Размер MAUI-APK пересобирается в тех же условиях, что Avalonia-APK (шаг 11). Число «около 35 МБ» из «Описания» в сравнение не идёт.
- iOS — только по документации, с пометкой «не проверено».
- Пробная голова берёт разметку экранов из `ImageGen/ImageGen.Avalonia` ссылками на файлы. Разделение оболочки на общую библиотеку и головы — только вывод в ADR, не работа этой задачи.

Обозначения: `POC` = `poc/backlog/094-avalonia-mobile-research/ImageGen/ImageGen.Avalonia.Android`, `AV` = `ImageGen/ImageGen.Avalonia`.

### Прочитай перед началом
- `poc/README.md`; `docs/tasks.md`, раздел «Завершение».
- `docs/knowledge/claude-cloud-session.md`, раздел «Как поставить» (.NET SDK, workload, Android SDK); `docs/knowledge/vps-session.md`, раздел «Отладка Android: реальное устройство по сети».
- `docs/knowledge/avalonia.md` целиком; `docs/decisions/0006-avalonia-desktop-and-presentation.md`, `docs/decisions/0008-desktop-secret-storage.md`.
- `AV/App.axaml`, `AV/App.axaml.cs`, `AV/Composition/AppServices.cs`, `AV/Views/MainWindow.axaml(.cs)`, `AV/Services/AvaloniaDialogService.cs`, `ImageGen/ImageGen.Maui/Services/MauiImageSaver.cs` (ветка `#if ANDROID`).
- «Анализ» этой задачи: таблица служб.

### Шаги
1. **Среда.** Выполни `dotnet --version` и `dotnet workload list`, проверь `echo $ANDROID_HOME`. Если нет SDK 10, workload `android` (или `maui-android`) или Android SDK с `platforms;android-36`, поставь их по `docs/knowledge/claude-cloud-session.md`, «Как поставить». Выполни `adb devices`. Запиши в «Отчёт»: версию SDK, workload, есть ли устройство (модель, версия Android из `adb shell getprop ro.build.version.release`).
2. **Шаблон для сверки** (только в `tmp/`, в репозиторий не копировать целиком). Выполни `dotnet new install Avalonia.Templates::12.1.4`, затем `dotnet new avalonia.xplat -n Probe -o tmp/094/xplat --MVVMToolkit CommunityToolkit`. Из `tmp/094/xplat/Probe.Android/` возьми как образец `MainActivity.cs`, `Application.cs`, `Resources/` (values, drawable и их варианты `-v31`, `-night`), `Icon.png` и `.csproj`. Если параметр `--MVVMToolkit` не принят, создай шаблон без него.
3. **Проект `POC/ImageGen.Avalonia.Android.csproj`** (SDK `Microsoft.NET.Sdk`):
   - свойства: `OutputType=Exe`, `TargetFramework=net10.0-android`, `SupportedOSPlatformVersion=23`, `AssemblyName=ImageGen.Avalonia` (нужно, чтобы адреса `avares://ImageGen.Avalonia/...` в линкованной разметке работали), `RootNamespace=ImageGen.Avalonia`, `ApplicationId=com.eliseev.imagegen.avaloniaprobe`, `ApplicationTitle=ImageGen Probe`, `ApplicationVersion=1`, `ApplicationDisplayVersion=1.0`, `AndroidPackageFormat=apk`, `AndroidEnableProfiledAot=false`, `Nullable=enable`, `ImplicitUsings=enable`, `AvaloniaUseCompiledBindingsByDefault=true`; свойство `RepoRoot=$(MSBuildThisFileDirectory)../../../../../`;
   - пакеты с явными версиями (центральное управление пакетами из `ImageGen/` на `poc/` не действует): `Avalonia.Android` 12.1.4, `Avalonia.Themes.Fluent` 12.1.4, `Microsoft.Extensions.DependencyInjection` той же версии, что в `ImageGen/Directory.Packages.props`, `Xamarin.AndroidX.Core.SplashScreen` той же версии, что в шаблоне шага 2;
   - ссылки на проекты: `$(RepoRoot)ImageGen/ImageGen.Core/ImageGen.Core.csproj`, `…/ImageGen.Data/ImageGen.Data.csproj`, `…/ImageGen.Presentation/ImageGen.Presentation.csproj`;
   - файлы из `AV` ссылками (`Link` — тот же относительный путь, что в `AV`): `Compile` — `Views/**/*.cs` без `Views/MainWindow.axaml.cs`, `Converters/**/*.cs`, `Markup/**/*.cs`, `Services/Navigation/**/*.cs`, `Services/AvaloniaMainThread.cs`, `Services/AvaloniaAppearanceApplier.cs`, `Services/Logging/**/*.cs`, `Services/Secrets/SecretFileNames.cs`; `AvaloniaXaml` — `Views/**/*.axaml` без `Views/MainWindow.axaml`, `Themes/**/*.axaml`; `AvaloniaResource` — `Assets/Fonts/**`, `Assets/Licenses/**`;
   - ресурсы Android и `Icon.png` — копии из шаблона шага 2 в `POC/Resources/` и `POC/Icon.png`.
   Отдельно собери проект: `dotnet build POC/ImageGen.Avalonia.Android.csproj -p:AndroidSdkDirectory=$ANDROID_HOME`. Ожидаемо упадёт: ещё нет `MobileApp` и служб. Ошибки компиляции в линкованных файлах из `AV` выпиши в «Отчёт» (файл, код ошибки) и исключи такой файл из ссылок. Если исключённый файл нужен экрану сессий или чата, запиши это как вывод «разметка не переносится без правок» и переходи к шагу 4 без этого экрана.
4. **`POC/MobileApp.axaml` и `POC/MobileApp.axaml.cs`** (`x:Class="ImageGen.Avalonia.MobileApp"`, `public partial class MobileApp : Application`). Ресурсы и стили — те же, что в `AV/App.axaml` (`ResourceInclude` для `Common.axaml` и `Quiet.axaml`, `ErrorBrush` в словарях Light и Dark, `FluentTheme`, `StyleInclude` для `Controls.axaml`). `OnFrameworkInitializationCompleted()` повторяет `AV/App.axaml.cs` по порядку: журнал, `StartupLog`, `CrashLog`, `AvaloniaLogSink`, `AvaloniaAppearanceApplier.Start()`. Отличия:
   - пути — `AndroidAppPaths`, секреты — `AndroidSecretStore`, контейнер — `MobileAppServices.Create(paths, secrets, secrets, configureLogging, debugTrace)`;
   - окна нет. Если `ApplicationLifetime is IActivityApplicationLifetime activity`, ставится `activity.MainViewFactory = CreateMainView`. Иначе, если `ApplicationLifetime is ISingleViewApplicationLifetime single`, ставится `single.MainView = CreateMainView()`;
   - контейнер хранится в свойстве экземпляра `public ServiceProvider? Services { get; private set; }`, как в `AV/App.axaml.cs`;
   - `private MainView CreateMainView()` создаёт `MainView` и сохраняет его в `public static MainView? CurrentView`. `AvaloniaNavigationService` — **не статический** класс: `AttachHost`, `CurrentRoute`, `GoToAsync`, `GoBackAsync` — члены экземпляра, а сам экземпляр — одиночка в DI (`AV/Composition/AppServices.cs`: `services.AddSingleton<AvaloniaNavigationService>()`). Поэтому экземпляр берётся из контейнера: `var navigation = Services.GetRequiredService<AvaloniaNavigationService>()` — так же, как в `AV/App.axaml.cs` (строки с `navigation.AttachHost(MainWindow.PageArea)`). Затем тот же экземпляр передаётся в `MainView` (свойство `Navigation`, шаг 5), вызывается `navigation.AttachHost(view.PageArea)`, и при первом вызове (флаг `_started`) — `navigation.GoToAsync(Routes.Sessions).FireAndForgetSafeAsync()`. Нового экземпляра через `new AvaloniaNavigationService(...)` не создавать: модели страниц навигируют через тот же одиночку (`INavigationService`).
5. **`POC/Views/MainView.axaml(.cs)`** (`public partial class MainView : UserControl`, пространство `ImageGen.Avalonia.Views`). Корень — `Grid` с двумя детьми: `ContentControl Name="PageHost"` и над ним `Grid Name="DialogLayer" IsVisible="False" Background="#80000000"`. Аксессоры: `public ContentControl PageArea` (через `FindControl`, как в `MainWindow`), `public Grid DialogArea`; свойство `public AvaloniaNavigationService? Navigation { get; set; }` — экземпляр навигации из контейнера, его ставит `MobileApp.CreateMainView` (шаг 4). В `OnAttachedToVisualTree` подпишись на `TopLevel.GetTopLevel(this)!.BackRequested`. Обработчик: если `DialogArea.IsVisible` — закрыть диалог как «Отмена», `Handled = true`. Иначе, если стек навигации глубже одной страницы (`Navigation` не `null` и `Navigation.CurrentRoute != Routes.Sessions`; `CurrentRoute` — свойство экземпляра), вызвать `Navigation.GoBackAsync().FireAndForgetSafeAsync()`, `Handled = true`. Иначе `Handled` не ставить: приложение уходит в фон. Если событие `BackRequested` не компилируется, запиши в «Отчёт» «нет `TopLevel.BackRequested`», удали подписку и продолжай.
6. **Android-голова:** `POC/MainActivity.cs` (`MainActivity : AvaloniaMainActivity`, атрибут `[Activity]` как в шаблоне, `Label = "ImageGen Probe"`, `MainLauncher = true`) и `POC/MainApplication.cs` (`[Application] MainApplication : AvaloniaAndroidApplication<MobileApp>`, конструктор `(nint, JniHandleOwnership)`, `CustomizeAppBuilder` возвращает `base.CustomizeAppBuilder(builder)` без изменений).
7. **Платформенные службы** (папка `POC/Platform/`, пространство `ImageGen.Avalonia.Platform`):
   - `AndroidAppPaths : IAppPaths`: `DataDirectory = Path.Combine(Android.App.Application.Context.FilesDir!.AbsolutePath, "ImageGen")`, папка создаётся в конструкторе. В журнал при старте пишется ещё и значение `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)` — для заметки в `docs/knowledge`;
   - `AndroidSecretStore : ISecretStore, ISecretPersistence`. Ключ AES-256 с псевдонимом `ImageGenSecrets` в провайдере `AndroidKeyStore` создаётся при первой записи: `KeyGenerator` с `KeyGenParameterSpec`, назначение Encrypt|Decrypt, режим GCM, без дополнения. Файл — `SecretFileNames.PathFor(DataDirectory, name)`; в нём 12 байт IV, затем шифротекст с тегом (`Cipher` `AES/GCM/NoPadding`, тег 128 бит). `GetAsync`: файла нет → `null`; исключение при расшифровке → `null` (как `MauiSecretStore`). `SetAsync` пишет файл. `RemoveAsync` удаляет файл. `IsPersistentAsync` → `true`;
   - `TopLevelClipboardService(Func<TopLevel?> owner) : IClipboardService` и `TopLevelFilePickerService(Func<TopLevel?> owner) : IFilePickerService` — копии `AvaloniaClipboardService` и `AvaloniaFilePickerService`, где `Window` заменён на `TopLevel`, а `window.StorageProvider` — на `topLevel.StorageProvider`;
   - `AndroidImageSaver : IImageSaver`: `ActionLabel = AppResources.SaveToGallery`. Ветка Android 10+ (`MediaStore`, `Pictures/ImageGen`, `IsPending`) переносится из `MauiImageSaver`, `Platform.AppContext` заменяется на `Android.App.Application.Context`. Ниже Android 10 → `ImageSaveResult.Failed` (в пробнике без запроса разрешения);
   - `MobileFolderLauncher : IFolderLauncher`: `IsSupported => false`, `OpenAsync` → `Task.CompletedTask`;
   - `OverlayDialogService(Func<MainView?> view) : IDialogService`: все пять методов интерфейса показывают в `MainView.DialogArea` по центру `Border` (`MaxWidth=360`). Внутри: заголовок `TextBlock`, текст `TextBlock` с переносом, для `PromptAsync` — `TextBox` с `initialValue`, для `ChooseAsync` — `ListBox` с вариантами. Ниже ряд кнопок accept и cancel, у кнопки и поля `AutomationProperties.Name` равен её тексту. Ответ ждёт `TaskCompletionSource`; при закрытии слой скрывается и очищается. `BackRequested` из шага 5 вызывает отмену через `internal void CancelOpenDialog()`.
8. **`POC/Composition/MobileAppServices.cs`** (`public static class MobileAppServices`, `public static ServiceProvider Create(IAppPaths paths, ISecretStore secrets, ISecretPersistence persistence, Action<ILoggingBuilder>? configureLogging = null, IDebugTrace? debugTrace = null)`). Копия `AV/Composition/AppServices.Create` с теми же маршрутами `PageRoutes`, моделями страниц и `ValidateOnBuild`. Отличия: `IDialogService` → `OverlayDialogService(() => MobileApp.CurrentView)`; `IFilePickerService`, `IClipboardService` → `TopLevel*`-службы с `() => TopLevel.GetTopLevel(MobileApp.CurrentView)`; `IImageSaver` → `AndroidImageSaver`; `IFolderLauncher` → `MobileFolderLauncher`. Без параметров `openRouterHandler` и `dialogs`.
9. **Клавиатура в чате** (разметку `AV/Views/ChatView.axaml` не править, всё задаётся из кода): в `POC/Views/MobileTweaks.cs` (`public static class MobileTweaks`, `public static void Apply(Control page)`) ищется `TextBox` с именем `Input` и ему задаются `TextInputOptions.SetReturnKeyType(input, TextInputReturnKeyType.Send)` и `TextInputOptions.SetMultiline(input, true)`. Вызов — в `MainView` при смене `PageHost.Content` (подписка на `PageHost.PropertyChanged` по `ContentControl.ContentProperty`). Отдельно подпишись на `TopLevel.InputPane.StateChanged`: при `InputPaneState.Open` выставляй `PageHost.Margin` снизу равным `OccludedRect.Height`, при закрытии — 0. Если `InputPane` равен `null`, запиши это в «Отчёт».
10. **Сборка Debug и запуск.** `dotnet build POC/ImageGen.Avalonia.Android.csproj -p:AndroidSdkDirectory=$ANDROID_HOME`. Если собирается — запиши «собирается» и число предупреждений. Если не собирается — выпиши ошибки в «Отчёт», выполни шаги 11 (только MAUI), 13 и 14 и закрой задачу с выводом «пробная голова не собралась» (вариант A в ADR). Если устройство есть (шаг 1), выполни `dotnet build … -t:Install` и `adb shell monkey -p com.eliseev.imagegen.avaloniaprobe 1`, затем `adb logcat -d | grep -i -E "avalonia|imagegen|FATAL"`. Падение при старте → стек из logcat в «Отчёт», вывод «не запускается», переходи к шагу 11.
11. **Размер APK в одинаковых условиях.** Обе сборки — `dotnet publish <csproj> -f net10.0-android -c Release -p:RuntimeIdentifiers=android-arm64 -p:AndroidPackageFormats=apk -p:AndroidSdkDirectory=$ANDROID_HOME` для `ImageGen/ImageGen.Maui/ImageGen.Maui.csproj` и `POC/ImageGen.Avalonia.Android.csproj`. Файл ищи командой `find <папка проекта>/bin/Release -name '*-Signed.apk'`, размер — `stat -c %s`. В «Отчёт» — таблица: проект, файл, байты, МБ (байты / 1048576, 1 знак). Состав больших частей: `unzip -l <apk> | sort -k1 -n -r | head -15` для обоих.
12. **Проверки на устройстве** (только если устройство есть; иначе все пункты — в «Ручную проверку»). Release-APK из шага 11 ставится через `adb install -r`. Результат каждого пункта — «да / нет / частично» с одной фразой в «Отчёт»:
    1. холодный старт: `adb shell am force-stop <пакет>`, затем `adb shell am start -W -n <пакет>/<активность>` три раза, медиана `TotalTime`, для MAUI и Avalonia. Активность — из `adb shell cmd package resolve-activity --brief <пакет>`;
    2. ключ: ввести любой текст на странице ключа, `adb shell am force-stop`, открыть снова — ключ сохранён (страница ключа не показывается). Затем `adb shell run-as com.eliseev.imagegen.avaloniaprobe ls files/ImageGen/secrets` — файл есть, `run-as … cat` — читаемого ключа нет;
    3. список сессий и чат открываются; создание сессии, переход в чат и «назад» системной кнопкой возвращают к списку; «назад» на списке сворачивает приложение;
    4. поле ввода чата: на экранной клавиатуре клавиша «Отправить», нажатие отправляет; поле видно над клавиатурой (сценарий задачи 095);
    5. буфер: «Copy» у сообщения, вставка в другое приложение;
    6. файл: скрепка открывает системный выбор, выбранное фото прикрепляется;
    7. галерея: если в ленте есть готовая картинка (нужен ключ владельца), «Save to gallery» → картинка в «Галерее», папка `Pictures/ImageGen`;
    8. диалог: удаление сессии спрашивает подтверждение слоем, «назад» его закрывает;
    9. узкий экран: шапка чата (задача 098), меню «…» карточки (088), боковая панель моделей не перекрывает ленту;
    10. память: `adb shell dumpsys meminfo <пакет> | grep TOTAL` после открытия чата, для обоих приложений.
13. **iOS по документации** (пометка «не проверено»): выполни `dotnet workload search ios` и `dotnet workload install ios` на Linux, вывод запиши. В заметку перенеси из `tmp/094/xplat/Probe.iOS/` состав iOS-головы: `AppDelegate`, `Main.cs`, `Info.plist`, `Entitlements.plist`. Ключи на iOS — Keychain через `Security.SecRecord`; галерея — `PHPhotoLibrary`, как в `MauiImageSaver`; сборка — только на macOS или через сетевой Mac-хост (docs.avaloniaui.net, Deployment → iOS).
14. **Документы:**
    - `docs/knowledge/avalonia-mobile.md` (новый). Разделы: «Версии и пакеты» (из «Анализа» и шага 1), «Шаблон и голова Android» (`AvaloniaAndroidApplication<TApp>`, `IActivityApplicationLifetime.MainViewFactory`, `AssemblyName` ради `avares://`), «Перенос разметки» (какие файлы из `AV` собрались без правок, какие нет), «Платформенные службы» (таблица из «Анализа» со столбцом «результат» и числом строк своего кода по каждой службе: `wc -l POC/Platform/*.cs POC/Composition/*.cs POC/Views/*.cs`), «Замеры» (шаги 11–12), «iOS» (шаг 13, «не проверено»). У каждого факта — дата и «проверено» или «не проверено»;
    - `docs/decisions/NNNN-mobile-shell.md` (следующий свободный номер в `docs/decisions/` в момент записи), по `_template.md`, `Статус: предложено`, строка «При принятии варианта B или C заменяет ADR 0006 в части MAUI». ADR 0006 не трогать. «Альтернативы» — варианты A (две оболочки), B (Avalonia везде), C (Avalonia на десктопе и Android, MAUI только на iOS); у каждого «выигрываем / теряем / объём переноса» — список экранов и служб из «Анализа». В «Решении» рекомендация по правилу. Обязательные критерии: голова собирается (шаг 10); запускается; ключ переживает перезапуск (12.2); «Отправить» с клавиатуры работает (12.4); Avalonia-APK не больше MAUI-APK + 10 МБ (шаг 11). Все выполнены → рекомендовать C, а B — после проверки iOS на Mac. Хоть один не выполнен → рекомендовать A. Хоть один «не проверено» (нет устройства) → рекомендовать A с пометкой «пересмотреть после проверки на устройстве»;
    - `docs/knowledge/dev-environment.md`, таблица «Что где собирается»: новая строка «Avalonia, цель Android (пробник 094)» с результатом сборки по средам, где проверял (остальные — «не проверено»);
    - `docs/index.md`, строка `knowledge/`: добавить `avalonia-mobile.md` (Avalonia на Android и iOS: исследование 094);
    - `docs/status.md`, «Открытые вопросы»: «Выбор мобильной оболочки (задача 094, ADR NNNN, предложено): A, B или C — решает владелец». Тот же вопрос — в «Комментарии» задачи.
15. **Отчёт задачи:** что собрано, что запущено, таблица APK, результаты шага 12, список «не проверено». Затем — по `docs/tasks.md`, «Завершение» (задача `done`, даже если устройства не было, — ручные пункты в «Ручной проверке» ниже).

### Проверка
```
dotnet build poc/backlog/094-avalonia-mobile-research/ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj -p:AndroidSdkDirectory=$ANDROID_HOME
dotnet publish ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android -c Release -p:RuntimeIdentifiers=android-arm64 -p:AndroidPackageFormats=apk -p:AndroidSdkDirectory=$ANDROID_HOME
dotnet publish poc/backlog/094-avalonia-mobile-research/ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj -f net10.0-android -c Release -p:RuntimeIdentifiers=android-arm64 -p:AndroidPackageFormats=apk -p:AndroidSdkDirectory=$ANDROID_HOME
adb devices
git status --short   # в ImageGen/ нет изменений, в poc/ нет bin/ и obj/
bash scripts/check.sh
```
Тесты и `dotnet format` не нужны: проекты в `ImageGen/` не меняются.

### Ручная проверка
Если устройства не было, пункты 12.1–12.10 переносятся в «Отчёт» как «не проверено, проверить владельцу», с командами из шага 12. Отдельно для владельца (всегда, потому что агент TalkBack не слушает): включить TalkBack и пройти список сессий, поле ввода, кнопку «Отправить», карточку с картинкой в обоих приложениях (MAUI и пробник), записать, что озвучено. Прокрутить ленту с ≥10 картинками в обоих приложениях и оценить «плавно / рывки». Результаты владелец дописывает в «Комментарии» задачи, исполнитель переносит их в `docs/knowledge/avalonia-mobile.md`. В задачи 016 и 046 ничего не добавляется: они про Windows и десктоп.

Файлы: `poc/backlog/094-avalonia-mobile-research/ImageGen/ImageGen.Avalonia.Android/**` (новые: `ImageGen.Avalonia.Android.csproj`, `MobileApp.axaml(.cs)`, `MainActivity.cs`, `MainApplication.cs`, `Views/MainView.axaml(.cs)`, `Views/MobileTweaks.cs`, `Platform/*.cs`, `Composition/MobileAppServices.cs`, `Resources/**`, `Icon.png`), `docs/knowledge/avalonia-mobile.md` (новый), `docs/decisions/NNNN-mobile-shell.md` (новый), `docs/knowledge/dev-environment.md`, `docs/index.md`, `docs/status.md`, `tasks/backlog/094-avalonia-mobile-research.md`.

## Отчёт

## Комментарии

### 2026-10-29, claude-desktop
Решение владельца в чате: пока поддерживаем обе оболочки (MAUI и Avalonia), пока не станет понятно, какая лучше. Задача не срочная и не блокирует 088–093: они делаются в обеих оболочках. Её результат — материал для этого выбора.

### 2026-10-29, claude-plan
Вопросы владельцу:
1. Есть ли Android-телефон, который можно подключить к VPS по `adb connect` (или к Windows-машине по USB)? Без него запуск, TalkBack, клавиатура и производительность останутся «не проверено»: эмулятора нет ни в одной среде.
2. Размер MAUI-APK около 35 МБ: какой это файл (APK или AAB, какие ABI)? Если неизвестно, план пересобирает MAUI-APK в тех же условиях, что Avalonia.
3. iOS: без Mac ни Avalonia, ни MAUI под iOS не собираются. Достаточно ли сравнения по документации, или вариант «Avalonia везде» нужно отложить до появления Mac?
4. Допущение: пробная голова подключает разметку из `ImageGen.Avalonia/Views` ссылками и не меняет `ImageGen/`; разделение оболочки на общую библиотеку и головы — только как вывод в ADR, не в этой задаче.

### 2026-10-29, владелец (в чате, записал claude-plan)
План одобрен; допущения из комментариев claude-plan приняты как решения. План переписан под небольшую модель.
### 2026-10-29, claude-review
Владелец попросил сделать задачу исполнимой небольшой моделью. Изменены шаги 4 и 5 «Плана»: `AvaloniaNavigationService.AttachHost(...)` и `.CurrentRoute` были написаны как статические, а это члены экземпляра (`ImageGen/ImageGen.Avalonia/Services/Navigation/AvaloniaNavigationService.cs`; экземпляр — одиночка из DI, его берёт `App.axaml.cs`). Теперь `MobileApp.CreateMainView` берёт экземпляр из своего `Services` (`GetRequiredService<AvaloniaNavigationService>()`), передаёт его в новое свойство `MainView.Navigation`, а обработчик «назад» пользуется этим экземпляром.
