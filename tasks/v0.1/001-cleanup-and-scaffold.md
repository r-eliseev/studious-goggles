---
id: 001
status: done
assignee: claude-qnrqyt
claimed: 2026-10-27T00:00:00Z
created: 2026-10-27
---

# Очистка репозитория и каркас решения

## Описание
Удалить всё, что осталось от проекта-примера и старых заготовок, и создать пустой каркас решения скелета v0.1: общие настройки сборки, центральные версии пакетов, пустой `ImageGen.Core`, тестовый проект с архитектурным тестом и новый CI (только задача `core`). После задачи CI зелёный, а в репозитории нет кода примера. Пустое MAUI-приложение и MAUI-сборку в CI добавляет задача 034.

Зависит от: нет.
Среда: Linux или Windows (нужен только .NET 10 SDK, MAUI-нагрузки не нужны).

## Критерии готовности
- Каталогов `ImageGen/ImageGen/` и `ImageGen/ImageGen.Core/Integration/` нет; файла `docs/knowledge/example-patterns.md` нет.
- Есть файлы из шага 2 плана с содержимым, как в плане.
- `ImageGen/ImageGen.slnx` содержит ровно два проекта: `ImageGen.Core`, `ImageGen.Core.Tests`.
- Все команды из раздела «Проверка» завершаются без ошибок.
- `.github/workflows/build.yml` совпадает с шагом 3.
- `AGENTS.md` и `docs/` не упоминают проект-пример и `example-patterns.md` (кроме `docs/log/` и ADR: их не трогать).
- `scripts/check.sh` проходит.

## Анализ
- Проект-пример больше не нужен: полезные приёмы перенесены в `docs/knowledge/maui.md` и `docs/knowledge/csharp.md` (сделано 2026-10-27 при подготовке задачи).
- Все версии пакетов скелета фиксируются здесь, чтобы следующие задачи не трогали `Directory.Packages.props`.
- Проверено пробной сборкой 2026-10-27 (`docs/knowledge/csharp.md`, раздел «Тесты»): xUnit v3 требует `global.json` с Microsoft.Testing.Platform.
- Правка `AGENTS.md` и `.github/workflows/build.yml` разрешена одобрением этого плана владельцем (см. `docs/status.md`, «Открытые вопросы»).
- Задача разделена с 034, чтобы её можно было выполнить в облачной сессии на Linux: MAUI-проект там не собирается (`docs/knowledge/dev-environment.md`). Между 001 и 034 в CI нет MAUI-сборки — это ожидаемо.

## План
1. Удалить из git:
   ```
   git rm -r ImageGen/ImageGen ImageGen/ImageGen.Core docs/knowledge/example-patterns.md
   ```
   Если на диске остались каталоги `ImageGen/ImageGen`, `ImageGen/ImageGen.Core`, `ImageGen/.vs` (не в git), удалить и их.

2. Создать файлы с таким содержимым (дословно).

   `.editorconfig` (корень репозитория):
   ```ini
   root = true

   [*]
   charset = utf-8
   insert_final_newline = true
   indent_style = space
   indent_size = 4
   trim_trailing_whitespace = true

   [*.{csproj,props,targets,xml,xaml,slnx,json,yml,yaml}]
   indent_size = 2

   [*.md]
   trim_trailing_whitespace = false

   [*.cs]
   csharp_style_namespace_declarations = file_scoped:warning
   dotnet_sort_system_directives_first = true
   ```

   `global.json` (корень репозитория):
   ```json
   {
     "test": {
       "runner": "Microsoft.Testing.Platform"
     }
   }
   ```

   `ImageGen/Directory.Build.props`:
   ```xml
   <Project>
     <PropertyGroup>
       <ImplicitUsings>enable</ImplicitUsings>
       <Nullable>enable</Nullable>
       <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
       <LangVersion>latest</LangVersion>
     </PropertyGroup>
   </Project>
   ```

   `ImageGen/Directory.Packages.props`:
   ```xml
   <Project>
     <PropertyGroup>
       <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
     </PropertyGroup>
     <ItemGroup>
       <PackageVersion Include="Microsoft.Maui.Controls" Version="10.0.110" />
       <PackageVersion Include="CommunityToolkit.Mvvm" Version="8.4.2" />
       <PackageVersion Include="CommunityToolkit.Maui" Version="15.0.1" />
       <PackageVersion Include="Microsoft.Extensions.Logging.Debug" Version="10.0.12" />
       <PackageVersion Include="Microsoft.Extensions.Http" Version="10.0.12" />
       <PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.12" />
       <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.12" />
       <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="10.0.12" />
       <PackageVersion Include="Microsoft.Data.Sqlite" Version="10.0.12" />
       <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.11.0" />
       <PackageVersion Include="xunit.v3" Version="4.0.2" />
       <PackageVersion Include="xunit.runner.visualstudio" Version="4.0.1" />
       <PackageVersion Include="NSubstitute" Version="6.2.0" />
     </ItemGroup>
   </Project>
   ```

   `ImageGen/ImageGen.Core/ImageGen.Core.csproj`:
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <TargetFramework>net10.0</TargetFramework>
     </PropertyGroup>
     <ItemGroup>
       <PackageReference Include="Microsoft.Extensions.Http" />
       <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
       <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
     </ItemGroup>
     <ItemGroup>
       <InternalsVisibleTo Include="ImageGen.Core.Tests" />
     </ItemGroup>
   </Project>
   ```

   `ImageGen/ImageGen.Core/CoreAssemblyMarker.cs`:
   ```csharp
   namespace ImageGen.Core;

   /// <summary>Marker type used to reference the ImageGen.Core assembly.</summary>
   public static class CoreAssemblyMarker
   {
   }
   ```

   `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs`:
   ```csharp
   using Microsoft.Extensions.DependencyInjection;

   namespace ImageGen.Core;

   public static class CoreServiceCollectionExtensions
   {
       public static IServiceCollection AddImageGenCore(this IServiceCollection services)
       {
           ArgumentNullException.ThrowIfNull(services);
           return services;
       }
   }
   ```

   `ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj`:
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <TargetFramework>net10.0</TargetFramework>
       <OutputType>Exe</OutputType>
       <IsPackable>false</IsPackable>
     </PropertyGroup>
     <ItemGroup>
       <PackageReference Include="Microsoft.NET.Test.Sdk" />
       <PackageReference Include="xunit.v3" />
       <PackageReference Include="xunit.runner.visualstudio" />
       <PackageReference Include="NSubstitute" />
     </ItemGroup>
     <ItemGroup>
       <Using Include="Xunit" />
       <Using Include="NSubstitute" />
     </ItemGroup>
     <ItemGroup>
       <ProjectReference Include="../ImageGen.Core/ImageGen.Core.csproj" />
     </ItemGroup>
   </Project>
   ```

   `ImageGen/ImageGen.Core.Tests/Architecture/CoreDependencyTests.cs`:
   ```csharp
   namespace ImageGen.Core.Tests.Architecture;

   public class CoreDependencyTests
   {
       private static readonly string[] ForbiddenPrefixes =
       [
           "Microsoft.Maui",
           "Microsoft.AspNetCore",
           "Microsoft.Data.Sqlite",
       ];

       [Fact]
       public void CoreAssembly_ReferencedAssemblies_DoNotIncludeUiOrStorage()
       {
           var referenced = typeof(CoreAssemblyMarker).Assembly
               .GetReferencedAssemblies()
               .Select(a => a.Name ?? string.Empty)
               .ToList();

           var forbidden = referenced
               .Where(name => ForbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
               .ToList();

           Assert.Empty(forbidden);
       }
   }
   ```

   `ImageGen/ImageGen.slnx` (перезаписать):
   ```xml
   <Solution>
     <Project Path="ImageGen.Core/ImageGen.Core.csproj" />
     <Project Path="ImageGen.Core.Tests/ImageGen.Core.Tests.csproj" />
   </Solution>
   ```

3. Перезаписать `.github/workflows/build.yml` (задачу `maui` вернёт задача 034):
   ```yaml
   name: build
   on:
     push:
       branches: [main]
     pull_request:

   jobs:
     core:
       runs-on: ubuntu-latest
       steps:
         - uses: actions/checkout@v4
         - uses: actions/setup-dotnet@v4
           with:
             dotnet-version: 10.0.x
         - run: dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
         - run: dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
         - run: dotnet build ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj -c Release
         - run: dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj -c Release --no-build
   ```

4. Обновить документацию (точные правки):
   - `AGENTS.md`, строка в «О проекте»: удалить последнее предложение «В `ImageGen/` сейчас только проект-пример (MAUI-шаблон), не продукт.» и после «Полная спецификация: `docs/specs/image-client.md`.» вставить «Основной сценарий (сессии с ассистентом): `docs/specs/chat-sessions.md`.»
   - `AGENTS.md`, раздел «Команды»: заменить пункты «Сборка», «Тесты», «Линтер/форматирование» на:
     ```
     - Среда: задачи ядра и хранилища идут на Linux или Windows, MAUI-задачи только на Windows (строка «Среда» в задаче, `docs/knowledge/dev-environment.md`)
     - Сборка ядра: `dotnet build ImageGen/ImageGen.Core/ImageGen.Core.csproj`; всё решение только на Windows с MAUI-нагрузками (долго): `dotnet build ImageGen/ImageGen.slnx`
     - Тесты: `dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj` и, после задачи 003, `dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj`
     - Сборка MAUI (Windows, после задачи 034): `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0` (и `-f net10.0-android`)
     - Запуск под Windows: после сборки `ImageGen/ImageGen.Maui/bin/Debug/net10.0-windows10.0.19041.0/win-x64/ImageGen.Maui.exe`
     - Линтер/форматирование: по каждому изменённому проекту `dotnet format ImageGen/<Проект>/<Проект>.csproj --verify-no-changes` (решение целиком не форматировать: на Linux MAUI-проект не загружается)
     ```
   - `AGENTS.md`, «Когда что обновлять»: «Нашёл новый неочевидный факт о C#/MAUI или о проекте-примере» заменить на «Нашёл новый неочевидный факт о C#/MAUI или о проекте».
   - `AGENTS.md`, «Главные правила кода»: удалить строку про паттерны проекта-примера и `example-patterns.md`.
   - `AGENTS.md`, «Запреты»: удалить строку «Не меняй `ImageGen/` (проект-пример) без явной просьбы…».
   - `docs/index.md`: в строке `architecture.md` текст «модули и потоки: целевая раскладка и состояние проекта-примера» заменить на «модули и потоки: скелет v0.1 и целевая раскладка»; в строке `knowledge/` удалить текст «, `example-patterns.md` (паттерны проекта-примера `ImageGen/`)» (вместе с запятой перед ним).
   - `docs/conventions.md`, первая строка после заголовка: удалить предложение «Паттерны проекта-примера: `docs/knowledge/example-patterns.md`.»; в разделе «Запреты» удалить строку «Не копировать из примера без правки: …».
   - `docs/specs/image-client.md`, строка «Код: …» заменить на «Код: скелет v0.1 в `ImageGen/` (`docs/architecture.md`)».
   - `docs/architecture.md`, раздел «Состояние кода»: текст раздела заменить на «Каркас скелета создан задачей 001: `ImageGen.Core` (пустой), `ImageGen.Core.Tests` (архитектурный тест). Пустое `ImageGen.Maui` из шаблона добавляет задача 034, остальное — задачи `tasks/v0.1/`.»
   - `docs/status.md`: абзац «Состояние проекта…» — фразу «Кода приложения нет: … создаёт каркас.» заменить на «Каркас решения создан задачей 001, кода приложения ещё нет.»; в «Известных проблемах» удалить пункты про проект-пример и «Тестовых проектов нет».
   - Других упоминаний проекта-примера в `AGENTS.md` и `docs/` (кроме `docs/log/` и `docs/decisions/`) быть не должно: проверка `grep -rn "проект-пример\|проекта-примера\|example-patterns" AGENTS.md docs --include=*.md | grep -v "docs/log/\|docs/decisions/"` ничего не выводит.

Файлы: `ImageGen/ImageGen/**` (удаление), `ImageGen/ImageGen.Core/**`, `ImageGen/ImageGen.Core.Tests/**`, `ImageGen/ImageGen.slnx`, `ImageGen/Directory.Build.props`, `ImageGen/Directory.Packages.props`, `.editorconfig`, `global.json`, `.github/workflows/build.yml`, `AGENTS.md`, `docs/index.md`, `docs/conventions.md`, `docs/architecture.md`, `docs/status.md`, `docs/specs/image-client.md`, `docs/knowledge/example-patterns.md` (удаление)

### Проверка
```
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
dotnet build ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
scripts/check.sh
```
Тест `CoreAssembly_ReferencedAssemblies_DoNotIncludeUiOrStorage` проходит (1 тест).

## Отчёт
- Удалены `ImageGen/ImageGen/`, старый `ImageGen/ImageGen.Core/` и `docs/knowledge/example-patterns.md` (через `git rm`).
- Созданы файлы из плана дословно: `.editorconfig`, `global.json`, `ImageGen/Directory.Build.props`, `ImageGen/Directory.Packages.props`, `ImageGen.Core` (csproj, маркер, пустой `AddImageGenCore`), `ImageGen.Core.Tests` (csproj и `CoreDependencyTests`), `ImageGen/ImageGen.slnx` (два проекта), `.github/workflows/build.yml` (задача `core`).
- Документация обновлена по списку плана (`AGENTS.md`, `docs/index.md`, `docs/conventions.md`, `docs/architecture.md`, `docs/status.md`, `docs/specs/image-client.md`). Проверка `grep` по «проект-пример / example-patterns» вне `docs/log/` и `docs/decisions/` пустая.
- Проверено на Linux (.NET SDK 10.0.403): `dotnet format ... --verify-no-changes` для обоих проектов — без изменений; `dotnet build` — 0 предупреждений, 0 ошибок; `dotnet test` — 1 тест, пройден; `scripts/check.sh` — пройден.
- Отличие от плана: занятость поставлена только в файле задачи на рабочей ветке. `scripts/claim-task.sh` пушит напрямую в `origin/main`, а задачи велись в ветке `ccr-81a5f3ba-qnrqyt`, поэтому скрипт не запускался.
- Отличие от плана: в `docs/status.md` строка про `planned` у задач осталась как была (это не входит в план 001).

## Комментарии
