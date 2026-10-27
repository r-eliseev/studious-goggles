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

## План
1. Из корня репозитория:
   ```
   dotnet new maui -n ImageGen.Maui -o ImageGen/ImageGen.Maui
   ```
   Сообщение шаблона об ошибке восстановления пакетов (`Restore failed`) ожидаемо: центральные версии ещё не подключены. Восстановление пройдёт при сборке после шага 2.

2. Изменить `ImageGen/ImageGen.Maui/ImageGen.Maui.csproj`, созданный шаблоном:
   - строку `<PackageReference Include="Microsoft.Maui.Controls" Version="$(MauiVersion)" />` заменить на три строки:
     ```xml
     <PackageReference Include="Microsoft.Maui.Controls" />
     <PackageReference Include="CommunityToolkit.Mvvm" />
     <PackageReference Include="CommunityToolkit.Maui" />
     ```
   - строку `<PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="10.0.0" />` заменить на `<PackageReference Include="Microsoft.Extensions.Logging.Debug" />`;
   - `<ApplicationTitle>ImageGen.Maui</ApplicationTitle>` заменить на `<ApplicationTitle>ImageGen</ApplicationTitle>`;
   - перед закрывающим `</Project>` добавить:
     ```xml
     <ItemGroup>
       <ProjectReference Include="../ImageGen.Core/ImageGen.Core.csproj" />
     </ItemGroup>
     ```
   Остальное в csproj не менять (в том числе `ApplicationId`).

3. В `ImageGen/ImageGen.Maui/MauiProgram.cs` добавить `using CommunityToolkit.Maui;` первой строкой и строку `.UseMauiCommunityToolkit()` сразу после `.UseMauiApp<App>()`.

4. В `ImageGen/ImageGen.slnx` перед строкой `</Solution>` добавить строку `  <Project Path="ImageGen.Maui/ImageGen.Maui.csproj" />`. Затем отформатировать шаблонные файлы: `dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj` (без `--verify-no-changes`).

5. В конец `.github/workflows/build.yml` (после задачи `core`, с пустой строкой перед ней) добавить задачу `maui`; отступы как у задачи `core`:
   ```yaml
     # Android собираем на Windows: установка нагрузки maui-android на ubuntu-latest падала с "Disk quota exceeded"
     maui:
       runs-on: windows-latest
       steps:
         - uses: actions/checkout@v4
         - uses: actions/setup-dotnet@v4
           with:
             dotnet-version: 10.0.x
         - run: dotnet workload install maui
         - run: dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
         - run: dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0 -c Release
         - run: dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android -c Release
   ```

6. В `docs/architecture.md`, раздел «Состояние кода»: фразу «Пустое `ImageGen.Maui` из шаблона добавляет задача 034» заменить на «Пустое `ImageGen.Maui` из шаблона добавлено задачей 034».

Файлы: `ImageGen/ImageGen.Maui/**`, `ImageGen/ImageGen.slnx`, `.github/workflows/build.yml`, `docs/architecture.md`

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
