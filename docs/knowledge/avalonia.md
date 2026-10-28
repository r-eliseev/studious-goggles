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

## Сборка
- `Avalonia.Desktop` подключает `UsePlatformDetect()`; на Linux без дисплея запуск окна не проверен (см. отчёт задачи 039).
