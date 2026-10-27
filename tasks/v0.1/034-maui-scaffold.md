---
id: 034
status: planned
assignee:
claimed:
created: 2026-10-27
---

# Пустое MAUI-приложение и MAUI-сборка в CI

## Описание
Создать проект `ImageGen.Maui` шаблоном `dotnet new maui`, подключить центральные версии пакетов, CommunityToolkit и ссылку на `ImageGen.Core`, добавить проект в решение и вернуть в CI задачу `maui` (сборка под Windows и Android на `windows-latest`). Отделено от задачи 001, потому что требует MAUI-нагрузок.

Зависит от: 001.
Среда: только Windows с установленными нагрузками MAUI (`dotnet workload list` показывает `maui-windows` и `android`).

## Критерии готовности
- `ImageGen/ImageGen.Maui/` создан шаблоном `dotnet new maui` и изменён только так, как сказано в шагах 2–3.
- `ImageGen/ImageGen.slnx` содержит строку проекта `ImageGen.Maui`.
- В `.github/workflows/build.yml` есть задача `maui` из шага 5, задача `core` не изменена.
- Все команды из раздела «Проверка» завершаются без ошибок и предупреждений.

## Анализ
- Проверено пробной сборкой 2026-10-27 (`docs/knowledge/maui.md`, раздел «Проверено пробной сборкой»): шаблон отформатирован табами (нужен один запуск `dotnet format`); `CommunityToolkit.Maui` без `.UseMauiCommunityToolkit()` даёт ошибку `MCT001`; при центральных версиях атрибут `Version` в `PackageReference` запрещён.
- Работать в клоне по короткому пути (например `C:\git\studious-goggles`): длинный путь ломает Windows-сборку (`MSB3030`).
- Шаблонная `MainPage` остаётся до задачи 013.
- Правка `.github/workflows/build.yml` разрешена одобрением этого плана владельцем.
- Итоговый csproj пробной сборки: `poc/v0.1/034-maui-scaffold/` (подсказка, см. `poc/README.md`).

## План
1. Из корня репозитория:
   ```
   dotnet new maui -n ImageGen.Maui -o ImageGen/ImageGen.Maui
   ```
   Сообщение шаблона об ошибке восстановления пакетов (`Restore failed`) ожидаемо: центральные версии ещё не подключены. Восстановление пройдёт при сборке после шага 2.

2. Изменить `ImageGen/ImageGen.Maui/ImageGen.Maui.csproj`, созданный шаблоном:
   - у всех `PackageReference` убрать атрибут `Version` (версии центральные, из `Directory.Packages.props`): это `Microsoft.Maui.Controls` и `Microsoft.Extensions.Logging.Debug`;
   - рядом с `Microsoft.Maui.Controls` добавить `PackageReference` на `CommunityToolkit.Mvvm` и `CommunityToolkit.Maui` (тоже без версии);
   - `ApplicationTitle` поменять с `ImageGen.Maui` на `ImageGen`;
   - в конец добавить `ItemGroup` с `ProjectReference` на `../ImageGen.Core/ImageGen.Core.csproj`.
   Остальное в csproj не менять (в том числе `ApplicationId`).

3. В `ImageGen/ImageGen.Maui/MauiProgram.cs` добавить `using CommunityToolkit.Maui;` первой строкой и строку `.UseMauiCommunityToolkit()` сразу после `.UseMauiApp<App>()`.

4. В `ImageGen/ImageGen.slnx` перед строкой `</Solution>` добавить строку `  <Project Path="ImageGen.Maui/ImageGen.Maui.csproj" />`. Затем отформатировать шаблонные файлы: `dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj` (без `--verify-no-changes`).

5. В конец `.github/workflows/build.yml` (после задачи `core`, через пустую строку, с теми же отступами) добавить задачу `maui` с комментарием, почему Android собирается на Windows (установка `maui-android` на ubuntu-latest падала с «Disk quota exceeded»). Задача: `runs-on: windows-latest`; шаги по порядку:
   - checkout и setup-dotnet с `dotnet-version: 10.0.x` (те же версии действий, что у `core`);
   - `dotnet workload install maui`;
   - `dotnet format` MAUI-проекта с `--verify-no-changes`;
   - сборка Release под `net10.0-windows10.0.19041.0`;
   - сборка Release под `net10.0-android`.

Файлы: `ImageGen/ImageGen.Maui/**`, `ImageGen/ImageGen.slnx`, `.github/workflows/build.yml`

### Проверка
```
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
scripts/check.sh
```

## Отчёт

## Комментарии
