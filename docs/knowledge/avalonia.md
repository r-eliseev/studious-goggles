# Avalonia: неочевидные факты

Факты, найденные при работе с `ImageGen.Avalonia` (задача 039). Проверено 2026-10-28 в облачной сессии (Linux), Avalonia 12.1.4, .NET 10.0.403. Общий обзор сред: `docs/knowledge/dev-environment.md`.

## Avalonia MAUI Backend

Оценка от 2026-10-28: `Avalonia.Controls.Maui` позволяет запускать MAUI-разметку внутри Avalonia и потенциально использовать одну оболочку для Linux и браузера. На дату проверки пакет был pre-release `11.0.0-rc.3`, требовал .NET 11 и MAUI 11, тогда как проект использует .NET 10. Были отмечены ограничения для `MainThread`, `SecureStorage`, доступности и Shell. Решение ADR 0006 не меняется: остаются две оболочки; пересматривать подход имеет смысл после стабильного релиза под актуальный стек.

## Android и iOS (исследование 094)

Проверено 2026-10-30. Avalonia 12.1.4, .NET SDK 10.0.403, Android target `net10.0-android36.0`, `Avalonia.Android` и `Avalonia.Themes.Fluent` восстановились и собрали пробник на Linux. Шаблон `Avalonia.Templates@12.1.4` создаёт Android/iOS/Desktop/Browser головы; Android использует `Application : AvaloniaAndroidApplication<App>`, `MainActivity : AvaloniaMainActivity` и `MainViewFactory`, потому что Android может создавать несколько Activity.

Пробник ссылками подключал актуальные `Views/*.axaml`, code-behind, Presentation, темы, шрифты, навигацию и платформенные службы к мобильной голове. Debug APK собирался без предупреждений. Живой запуск не проверен (`adb devices` был пуст); не проверены KeyStore, backup/restore, клавиатура, Back, clipboard, picker, галерея, overlay и TalkBack. Сценарии переданы в задачу 140. APK Release `android-arm64` был 29,152,195 байт (27.8 МиБ), на 6,968,679 байт (6.65 МиБ) больше MAUI APK 22,183,516 байт (21.2 МиБ); фактический размер установки не измерен.

iOS проверена только по официальной документации Avalonia на 2026-10-30: сборка требует Mac и Xcode (либо удалённый Mac build host). Пробник содержал шаблонную iOS голову, но workload и сборка не проверялись; устройства и Mac не было.

## Тесты
- `Avalonia.Headless.XUnit` 12.1.4 не работает с `xunit.v3` 4.0.2: при обнаружении тестов `MissingMethodException`. Решение и обход — `docs/decisions/0007-avalonia-tests-xunit-3-2.md` (`VersionOverride` 3.2.2 только в `ImageGen.Avalonia.Tests`).
- Сборка тестов headless идёт без экрана и без X11 (проверено на этой машине).
- В headless-окружении `Application.ApplicationLifetime` равен `null`: окно, которое создаётся только через `IClassicDesktopStyleApplicationLifetime`, в тестах не появляется. Поэтому `App` создаёт `MainWindow` всегда и сохраняет его в `App.MainWindow`.
- `Dispatcher.UIThread.RunJobs()` выполняет отложенные действия (`Post`) в тестах; `Dispatcher.UIThread.CheckAccess()` подтверждает поток.

## XAML
- Пространство имён из другой сборки (`ImageGen.Presentation`) в разметке Avalonia пишется с квалификатором: `xmlns:pm="clr-namespace:ImageGen.Presentation.PageModels;assembly=ImageGen.Presentation"`. Без `;assembly=` компилятор даёт `AVLN2000` («Unable to resolve type … from namespace»), даже когда сборка подключена. То же касалось `x:Static` на `AppResources`.
- Строки интерфейса в разметке: расширение `{markup:Res Ключ}` (`ImageGen.Avalonia/Markup/ResExtension.cs`), читает свойство `AppResources`. Заголовок окна задаётся в коде: `Title = AppResources.AppTitle`.
- `x:Name="PageHost"` генерирует член `PageHost` (внутренний или приватный). Свой публичный аксессор с тем же именем даёт `CS0102`; поэтому у `MainWindow` аксессор называется `PageArea`, а в XAML стоит `Name="PageHost"` для `FindControl`.
- `TextBox.Watermark` помечен устаревшим: `PlaceholderText` (предупреждение AVLN5001 при сборке с `TreatWarningsAsErrors`).
- `AvaloniaUseCompiledBindingsByDefault=true` включён; привязки в разметке нужно снабжать `x:DataType`. Режим `Mode=TwoWay` для `TextBox.Text` указываем явно.
- Внутри `namespace ImageGen.Avalonia` полные имена `Avalonia.Controls.X` не разрешаются (тень пространства имён): используем `using`.

## Чат и карточки (задача 042)
- `Bitmap(path)` для файла, который не картинка, не бросает исключение: даёт битмап 1×1 (проверено на 12.1.4). Конвертер `FilePathToBitmapConverter` всё равно возвращает `null` для пустого пути и отсутствующего файла.
- На Windows `Bitmap(path)` может удерживать исходный файл открытым, пока жив bitmap; это мешает удалять PNG из временной папки сразу после закрытия UI. Для карточек декодировать байты из памяти (`Bitmap(Stream)`), чтобы не блокировать файл на диске. Проверено по Windows CI 2026-10-30; Avalonia viewer уже читает байты и передаёт `MemoryStream`.
- Элементы шаблонов `ControlTemplate` (например, кнопки счётчика `NumericUpDown`) имеют `TemplatedParent`; при проверке имён доступности их пропускаем.
- Визуальное дерево новой страницы появляется после прохода компоновки: `Layoutable.UpdateLayout()` в тесте.
- Снимок окна в headless: `UseSkia()` и `UseHeadlessDrawing = false` в `AvaloniaHeadlessPlatformOptions`, затем `AvaloniaHeadlessPlatform.ForceRenderTimerTick()` и `CaptureRenderedFrame()`. В обычных тестах не используется: отрисовка замедляет и не нужна проверкам логики.
- Прокрутка ленты: `ScrollViewer.ScrollToEnd()` через `Dispatcher.UIThread.Post` после изменения коллекции (иначе новая строка ещё не имеет размера).

## Навигация и диалоги (задача 041)
- Стек страниц в `ContentControl` главного окна (`AvaloniaNavigationService`); маршруты — те же строки `Routes`, что в MAUI. Страница, которую показали, получает `OnAppearing` (интерфейс `IAppearing`), в том числе при возврате назад.
- Диалоги (`AvaloniaDialogService`) собираются в коде, а не в XAML: окно с текстом, полем и кнопками, у каждого контрола `AutomationProperties.Name`. Ожидание ответа — через `TaskCompletionSource`; окно немодальное (владелец задан, модальности нет). В headless-тестах диалоги подменяются.

## Сборка и запуск
- `Avalonia.Desktop` подключает `UsePlatformDetect()`. Запуск на Linux без дисплея: `xvfb-run -a <программа>`; окно можно снять `import -window root <файл.png>` (ImageMagick). Проверено 2026-10-28: окно 960×640 создаётся и показывается, процесс живёт без исключений.

## Секреты на десктопе (ADR 0008)
- Пакет `gnome-keyring` ставится через apt (Ubuntu 24.04). Служба `org.freedesktop.secrets` поднимается D-Bus-активацией внутри сессии `dbus-run-session`.
- Коллекция `default` (алиас `login`) в контейнере не создаётся: демон запрашивает пароль через `org.gnome.keyring.SystemPrompter`, а для промпта нужен дисплей. Коллекция `session` доступна без промпта: её используют тесты `SecretServiceBackendTests`. На настоящем рабочем столе `default` есть.
- В `Tmds.DBus` 0.95.1 вариант (`v`) передаётся как `object`, словари `a{ss}` и `a{sv}` — как `IDictionary<string, string>` и `IDictionary<string, object>`. Подключение: `Connection.Session` и `ConnectAsync()`.
- Тесты Secret Service пропускаются без сессии D-Bus (`Assert.Skip`); в CI их нет. Внутри `dbus-run-session` с gnome-keyring все тесты проходят: 15 из 16, пропущен DPAPI (нужен Windows).
- macOS-бэкенд и DPAPI на Windows не проверены: нет машин.

## Enter в поле чата (задача 050)
- Обработчик `KeyDown`, объявленный в XAML, на `TextBox` с `AcceptsReturn` не успевает перехватить Enter: поле вставляет перевод строки раньше. Поэтому обработчик добавлен из кода конструктора туннельным (`AddHandler(KeyDownEvent, …, RoutingStrategies.Tunnel)`) и ставит `Handled` на Enter без Shift.
- В headless-тестах клавиша поднимается через `RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, … })`; перевод строки от клавиатуры в headless не моделируется, проверяется `Handled`.
- **Смена стиля и темы без перезапуска (задача 061, проверено headless):** стили — словари `Themes/Quiet|Gallery|Notebook.axaml`, подключены через `MergedDictionaries` в `App.axaml` (`ResourceInclude`); `AvaloniaAppearanceApplier` меняет элемент этого списка и ставит `Application.RequestedThemeVariant`. Словарь темы — `ResourceDictionary.ThemeDictionaries[ThemeVariant.Light|Dark]`, в XAML ключи `x:Key="Light"`/`"Dark"`. Перечисления (`HorizontalAlignment`, `FontStyle`) как ресурсы в XAML не объявляются, поэтому хранятся строками (`x:String`); экраны переводят их сами.
- **Базовые стили Avalonia (задача 063):** `Themes/Controls.axaml` подключён в `App.axaml` через `StyleInclude` после `FluentTheme`; классы кнопок, полей, текста, значков и статусов берут цвета и размеры через `DynamicResource Ig*`. Фокус с клавиатуры — `:focus-visible` (проверено в headless: стиль загружается, обводка задаётся `BorderBrush`); для флажка и переключателя фон и цвет меняются через `/template/` части `PART_Border` и `OnContentBorder`.

## Клавиши и меню (задача 073)
- Модификатор «основной клавиши» — `PlatformKeys.Primary` (`Meta` на macOS, `Control` иначе). `KeyGesture` сравнивается через `Equals`; в тестах `Assert.Same` на команде привязки работает, потому что команды страниц — синглтоны.
- Сочетания окна — `Window.KeyBindings` (в `MainWindow.ConfigureShortcuts`), подписи пунктов меню — `MenuItem.InputGesture` (контекстное меню карточки). Сочетания карточки — `KeyBinding` на `Border` карточки с `Focusable="True"`: работают, только когда фокус внутри карточки; поле ввода вне карточки, поэтому буква `R` там печатается.
- `NativeMenu.SetMenu(window, …)` строит меню приложения на macOS. **Не проверено:** на macOS меню и `KeyBindings` могут сработать дважды на одно сочетание; на Windows и Linux `NativeMenu` без `NativeMenuBar` не показывается (неизвестно, сохраняется ли это в текущей версии Avalonia).
- Фокус на поле поиска: `SessionsListView.FocusSearch()` → `TextBox.Focus()`; в headless проверяется `IsFocused`.

