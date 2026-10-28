# Avalonia: неочевидные факты

Факты, найденные при работе с `ImageGen.Avalonia` (задача 039). Проверено 2026-10-28 в облачной сессии (Linux), Avalonia 12.1.4, .NET 10.0.403. Общий обзор сред: `docs/knowledge/dev-environment.md`.

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
