# Avalonia: неочевидные факты

Факты, найденные при работе с `ImageGen.Avalonia` (задача 039). Проверено 2026-10-28 в облачной сессии (Linux), Avalonia 12.1.4, .NET 10.0.403. Общий обзор сред: `docs/knowledge/dev-environment.md`.

## Тесты
- `Avalonia.Headless.XUnit` 12.1.4 не работает с `xunit.v3` 4.0.2: при обнаружении тестов `MissingMethodException`. Решение и обход — `docs/decisions/0007-avalonia-tests-xunit-3-2.md` (`VersionOverride` 3.2.2 только в `ImageGen.Avalonia.Tests`).
- Сборка тестов headless идёт без экрана и без X11 (проверено на этой машине).
- В headless-окружении `Application.ApplicationLifetime` равен `null`: окно, которое создаётся только через `IClassicDesktopStyleApplicationLifetime`, в тестах не появляется. Поэтому `App` создаёт `MainWindow` всегда и сохраняет его в `App.MainWindow`.
- `Dispatcher.UIThread.RunJobs()` выполняет отложенные действия (`Post`) в тестах; `Dispatcher.UIThread.CheckAccess()` подтверждает поток.

## XAML
- `x:Static` на класс из другой сборки (`ImageGen.Presentation`) компилятор XAML Avalonia 12.1.4 не разрешает: ошибка `AVLN2000`, хотя класс публичный и сборка подключена. Заголовок окна задаётся в коде: `Title = AppResources.AppTitle`.
- `AvaloniaUseCompiledBindingsByDefault=true` включён; привязки в разметке нужно снабжать `x:DataType`.

## Сборка и запуск
- `Avalonia.Desktop` подключает `UsePlatformDetect()`. Запуск на Linux без дисплея: `xvfb-run -a <программа>`; окно можно снять `import -window root <файл.png>` (ImageMagick). Проверено 2026-10-28: окно 960×640 создаётся и показывается, процесс живёт без исключений.

## Секреты на десктопе (ADR 0008)
- Пакет `gnome-keyring` ставится через apt (Ubuntu 24.04). Служба `org.freedesktop.secrets` поднимается D-Bus-активацией внутри сессии `dbus-run-session`.
- Коллекция `default` (алиас `login`) в контейнере не создаётся: демон запрашивает пароль через `org.gnome.keyring.SystemPrompter`, а для промпта нужен дисплей. Коллекция `session` доступна без промпта: её используют тесты `SecretServiceBackendTests`. На настоящем рабочем столе `default` есть.
- В `Tmds.DBus` 0.95.1 вариант (`v`) передаётся как `object`, словари `a{ss}` и `a{sv}` — как `IDictionary<string, string>` и `IDictionary<string, object>`. Подключение: `Connection.Session` и `ConnectAsync()`.
- Тесты Secret Service пропускаются без сессии D-Bus (`Assert.Skip`); в CI их нет. Внутри `dbus-run-session` с gnome-keyring все тесты проходят: 15 из 16, пропущен DPAPI (нужен Windows).
- macOS-бэкенд и DPAPI на Windows не проверены: нет машин.
