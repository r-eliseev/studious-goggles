---
id: 039
status: in-progress
assignee: claude-qnrqyt
claimed: 2026-10-28T00:00:00Z
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
- Новые проекты: `ImageGen.Avalonia` (WinExe, `net10.0`, Avalonia 12.1.4, Fluent, `AvaloniaUseCompiledBindingsByDefault`; ссылки на Core, Data, Presentation) и `ImageGen.Avalonia.Tests` (headless, Exe).
- `ImageGen/Directory.Packages.props`: Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent, Avalonia.Headless.XUnit (12.1.4). `Avalonia.Fonts.Inter` добавлен в список версий, но в проект не подключён (см. отклонения).
- Состав: `Program` (`UsePlatformDetect`, classic desktop lifetime), `App` (собирает контейнер и создаёт `MainWindow`), `Views/MainWindow` (пустой `PageHost` под навигацию 041), `Composition/AppServices` (`AddImageGenCore`, `AddImageGenData`, `AppInitializer`, page models; `ValidateOnBuild`, `ValidateScopes`), `Platform/DesktopAppPaths` (`%LOCALAPPDATA%/ImageGen`, как в плане), `Platform/InMemorySecretStore` (до 040), `Services/AvaloniaMainThread` (`Dispatcher.UIThread.Post`), `Services/NotYetAvailable` (заглушки `INavigationService` и `IDialogService`: любой вызов бросает `NotSupportedException`, до 041).
- TDD: тесты написаны первыми; до создания проекта сборка падала (CS0234/CS0246 на `ImageGen.Avalonia`), после реализации тесты прошли.
- Тесты (4/4): `AppServicesTests.Create_ValidateOnBuild_ResolvesPageModels`; `MainWindowTests.Show_Window_HasAppTitleAndIsVisible`; `AvaloniaMainThreadTests.BeginInvokeOnMainThread_RunsActionOnUiThread`; `AppStartupTests.Startup_BuildsContainerAndMainWindow`.
- Отклонения от плана:
  - `xunit.v3` в `ImageGen.Avalonia.Tests` закреплён на 3.2.2 (`VersionOverride`): `Avalonia.Headless.XUnit` 12.1.4 не работает с xunit v3 4.0.2. Решение и обход — ADR 0007 и комментарий ниже.
  - `MainWindow.Title` задаётся в коде (`AppResources.AppTitle`), а не через `x:Static`: компилятор XAML Avalonia не разрешает `x:Static` на тип из `ImageGen.Presentation` (AVLN2000).
  - `App.MainWindow` создаётся всегда: в headless `ApplicationLifetime` равен `null`, окно через lifetime в тестах не появлялось.
  - `Avalonia.Fonts.Inter` не подключён: в плане не указано, какой URI шрифта; Inter добавляем вместе с настройкой шрифтов, пока не нужен.
- Результаты (Linux, облачная сессия):
  - `dotnet build -c Release ImageGen.Avalonia.Tests`: 0 предупреждений, 0 ошибок (предупреждения считаются с `TreatWarningsAsErrors`).
  - `dotnet test --no-build` (Release): 4/4.
  - `dotnet format --verify-no-changes` для `ImageGen.Avalonia` и `ImageGen.Avalonia.Tests`: код 0.
  - Запуск `ImageGen.Avalonia` под Xvfb (`xvfb-run`): процесс работает 25 с без исключений; снимок экрана показывает окно 960×640 (белая область) — `tmp/avalonia-startup.png`.
  - `scripts/check.sh`: пройден.
- Добавлено в CI: задача `avalonia` (ubuntu-latest): format обоих проектов, build и test (Release).
- Документация: `AGENTS.md` (команды Avalonia и Presentation-тестов), `docs/architecture.md` (текущая раскладка, зависимости), `docs/knowledge/dev-environment.md` (строка таблицы), `docs/knowledge/avalonia.md` (новый, неочевидные факты), `docs/index.md`, `docs/decisions/0007-avalonia-tests-xunit-3-2.md` (новый ADR о версии xunit).
- Не проверено: сборка и запуск на Windows и macOS (нет машин; Windows-часть — отдельная проверка); задача `avalonia` в GitHub Actions (локально повторены её шаги). Поэтому статус `in-progress`.
- Среда: Linux (облачная сессия) с Xvfb.

## Комментарии

### 2026-10-28, планирование (claude)
Вопрос владельцу: на одной Windows-машине MAUI и Avalonia хранят данные в разных папках (MAUI — `%LOCALAPPDATA%\User Name\com.companyname.imagegen.maui\Data`, Avalonia — `%LOCALAPPDATA%\ImageGen`), то есть сессии и ключ у них разные. Это нормально или нужна общая база? Допущение плана: разные папки.

### 2026-10-28, владелец (в чате, записал ревью)
Разные папки данных у MAUI и Avalonia — подтверждено.

### 2026-10-28, владелец (в чате, записал ревью)
План одобрен.

### 2026-10-28, claude-qnrqyt (несовместимость xunit)
Проверка сборкой (шаг 1 плана) выявила несовместимость: `Avalonia.Headless.XUnit` 12.1.4 при обнаружении тестов падает с `MissingMethodException` (`TestIntrospectionHelper.GetTestCaseDetails`) под `xunit.v3` 4.0.2 из центрального списка. Обход, который принят: `VersionOverride="3.2.2"` для `xunit.v3` только в `ImageGen.Avalonia.Tests` (ADR 0007). Если владелец против, варианты в ADR 0007: общая версия xunit для всего решения или тесты без `Avalonia.Headless.XUnit`.
