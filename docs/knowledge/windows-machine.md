# Windows-машина разработки

Описание среды, на которой собирается цель Windows у MAUI и проверяется интерфейс. Общий обзор сред: `docs/knowledge/dev-environment.md`. Записано 2026-10-27 по прежнему описанию среды. В ревизии облачной сессии эта машина не проверялась: всё ниже «не проверено» на самой машине.

## Что нужно
- Windows с установленными нагрузками MAUI (`maui-windows`, `maui-android`), .NET 10 SDK.
- Для Android-цели: Android SDK и JDK (не проверено на этой машине).
- Для проверки скелета (задача 016) нужен человек у экрана: интерфейс проверяется вручную.

## Что собирается только здесь
- Цель `net10.0-windows10.0.19041.0` у `ImageGen.Maui`. На Linux она не собирается: XAML-компилятор WinUI работает только под Windows (проверено в облачной сессии, см. `docs/knowledge/claude-cloud-session.md`).
- Ручная проверка интерфейса скелета (задача 016) — только на Windows.

## Команды
- Сборка Windows: `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0`
- Сборка Android: `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android`
- Запуск: `ImageGen/ImageGen.Maui/bin/Debug/net10.0-windows10.0.19041.0/win-x64/ImageGen.Maui.exe`
- Форматирование по проекту: `dotnet format ImageGen/<Проект>/<Проект>.csproj --verify-no-changes`
