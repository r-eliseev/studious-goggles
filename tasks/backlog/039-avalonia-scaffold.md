---
id: 039
status: planned
assignee:
claimed:
created: 2026-10-28
---

# Avalonia: пустое десктопное приложение, тесты и CI

## Описание
Создать `ImageGen.Avalonia` (десктоп: Linux, Windows, macOS) и `ImageGen.Avalonia.Tests` (headless) по ADR 0006: приложение запускается, открывает главное окно, собирает контейнер DI с ядром, хранилищем и Presentation. Экранов ещё нет (задачи 041, 042), ключ пока только в памяти (задача 040). Добавить задачу `avalonia` в CI на Linux.

Зависит от: 038.
Среда: Linux или Windows (Avalonia-десктоп собирается и тестируется на Linux).

## Критерии готовности
- `dotnet build` и `dotnet test` для `ImageGen.Avalonia` и `ImageGen.Avalonia.Tests` проходят на Linux без предупреждений.
- Headless-тест: приложение стартует, главное окно создано, контейнер DI разрешает `SessionsPageModel` (из Presentation) с проверкой при построении.
- В CI есть задача `avalonia` (ubuntu-latest): format, build, test.
- `docs/knowledge/dev-environment.md` и `AGENTS.md` («Команды») знают про Avalonia.

## Анализ
- Avalonia 12.1.4 (nuget.org, обновлено 2026-10-09), цель `net10.0` есть. `Avalonia.Headless.XUnit` 12.1.4 зависит от `xunit.v3.extensibility.core` (>= 3.2.2). Совместимость с закреплёнными в `Directory.Packages.props` `xunit.v3` и `Microsoft.Testing.Platform` (`global.json`) не проверена: первый шаг плана — проверить сборкой и при несовместимости записать в «Комментарии», а не подбирать версии молча.
- Шаблоны Avalonia (`dotnet new avalonia.app`) ставятся отдельным пакетом `Avalonia.Templates`; можно и без шаблона — проект небольшой. Решение при реализации; результат — проект по правилам `docs/conventions.md` (центральные версии пакетов, `TreatWarningsAsErrors`).
- Документация Avalonia по headless-тестам: `docs.avaloniaui.net/docs/testing/headless-xunit` — `[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]`, `AppBuilder.Configure<App>().UseHeadless(...)`, тесты `[AvaloniaFact]`.
- Компилируемые привязки: свойство `AvaloniaUseCompiledBindingsByDefault` и `x:DataType` в разметке (аналог `x:DataType` в MAUI, правило `AGENTS.md`). Доступность: `AutomationProperties.Name` и `AutomationProperties.HelpText` (аналог `SemanticProperties`).
- `IAppPaths` для десктопа: `Environment.SpecialFolder.LocalApplicationData` + `ImageGen` (на Linux это `~/.local/share/ImageGen`). Папка данных не совпадает с папкой MAUI на Windows: у двух оболочек разные базы. Общая ли база должна быть у MAUI и Avalonia на одной Windows-машине — вопрос владельцу (комментарий).

## План
1. `Directory.Packages.props`: версии `Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`, `Avalonia.Fonts.Inter`, `Avalonia.Headless.XUnit` (12.1.4), `Microsoft.Extensions.DependencyInjection` уже есть.
2. `ImageGen/ImageGen.Avalonia/`: `net10.0`, `OutputType WinExe`, `AvaloniaUseCompiledBindingsByDefault=true`; ссылки на Core, Data, Presentation.
   - `Program.Main` → `BuildAvaloniaApp().StartWithClassicDesktopLifetime(args)`.
   - `App` (Fluent-тема): при запуске собирает `ServiceProvider` (`AddImageGenCore`, `AddImageGenData`, Presentation-сервисы, `IAppPaths` → `DesktopAppPaths`, `ISecretStore` → `InMemorySecretStore` до задачи 040, `IMainThread` → `AvaloniaMainThread` через `Dispatcher.UIThread.Post`), создаёт `MainWindow`.
   - `MainWindow`: заголовок `AppResources.AppTitle`, пустая область содержимого (`ContentControl`), куда навигация 041 будет ставить страницы.
3. `ImageGen/ImageGen.Avalonia.Tests/`: headless по документации; `TestAppBuilder`; тесты:
   - главное окно создаётся и показывается, заголовок `ImageGen`;
   - контейнер собирается с `ValidateOnBuild` и разрешает `SessionsPageModel`, `ChatPageModel`, `ApiKeyPageModel`;
   - `AvaloniaMainThread.Post` выполняет действие на потоке диспетчера.
4. `ImageGen.slnx`; `.github/workflows/build.yml` — задача `avalonia` на `ubuntu-latest`: setup-dotnet 10.0.x, format обоих проектов, build, test.
5. Документация: `docs/knowledge/dev-environment.md` (строка таблицы «`ImageGen.Avalonia` — Linux, Windows; macOS не проверено»), `AGENTS.md` («Команды»: сборка, тесты и запуск Avalonia), `docs/architecture.md` (раскладка), `docs/knowledge/avalonia.md` (новый: неочевидные факты, найденные при работе; добавить в `docs/index.md`).

### Команды проверки
```
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj --verify-no-changes
dotnet build ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
scripts/check.sh
```

Файлы: `ImageGen/Directory.Packages.props`, `ImageGen/ImageGen.Avalonia/**` (новый), `ImageGen/ImageGen.Avalonia.Tests/**` (новый), `ImageGen/ImageGen.slnx`, `.github/workflows/build.yml`, `AGENTS.md`, `docs/architecture.md`, `docs/knowledge/dev-environment.md`, `docs/knowledge/avalonia.md` (новый), `docs/index.md`

## Отчёт

## Комментарии

### 2026-10-28, планирование (claude)
Вопрос владельцу: на одной Windows-машине MAUI и Avalonia хранят данные в разных папках (MAUI — `%LOCALAPPDATA%\User Name\com.companyname.imagegen.maui\Data`, Avalonia — `%LOCALAPPDATA%\ImageGen`), то есть сессии и ключ у них разные. Это нормально или нужна общая база? Допущение плана: разные папки.
