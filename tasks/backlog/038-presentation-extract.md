---
id: 038
status: approved
assignee:
claimed:
created: 2026-10-28
---

# Общий слой представления ImageGen.Presentation

## Описание
Вынести из `ImageGen.Maui` всё, что не зависит от MAUI, в новый проект `ImageGen.Presentation` (ADR 0006): модели страниц и строк, интерфейсы навигации, диалогов и UI-потока, маршруты, `AppInitializer`, `FireAndForgetSafeAsync`, строки `.resx`. Тесты из `MauiLinked/` переезжают в `ImageGen.Presentation.Tests`, подключение файлов ссылками убирается. Поведение приложения не меняется.

Зависит от: 035 (очередь владельца); по коду зависимостей нет.
Среда: Linux (с Android SDK, `docs/knowledge/claude-cloud-session.md`) или Windows. Разметка MAUI меняется (пространства имён), поэтому нужна сборка MAUI хотя бы под Android; Windows-цель проверяет CI.

## Критерии готовности
- `ImageGen.Presentation` и `ImageGen.Presentation.Tests` есть в решении и в CI (задача `core`).
- В `ImageGen.Core.Tests` и `ImageGen.Data.Tests` нет `<Compile Include="../ImageGen.Maui/...">`, нет каталогов `MauiLinked/` и ссылки на `CommunityToolkit.Mvvm`.
- Все прежние тесты моделей страниц проходят в `ImageGen.Presentation.Tests`; общее число тестов не уменьшилось.
- MAUI собирается под Android (и под Windows в CI) без предупреждений; поведение страниц не изменилось.
- Архитектурный тест: `ImageGen.Presentation` не ссылается на `Microsoft.Maui*`, `Avalonia*`, `Microsoft.AspNetCore*`.

## Анализ
- Сейчас платформонезависимые файлы лежат в `ImageGen.Maui` и компилируются ещё и в `ImageGen.Core.Tests` (модели страниц, `Routes`, `AppResources`, `IDialogService`, `INavigationService`, `IMainThread`, `TaskExtensions`) и в `ImageGen.Data.Tests` (`AppInitializer`, `SessionsPageModel`, `SessionItemViewModel` и др.) через `Compile Link` (`docs/architecture.md`, `docs/conventions.md`).
- Остаются в MAUI: `Pages/*`, `AppShell`, `App`, `MauiProgram`, `Converters/FilePathToImageSourceConverter`, `ChatItemTemplateSelector` (это `DataTemplateSelector` MAUI; логика выбора уже в `ChatItemTemplateChoice`), `Services/MauiAppPaths`, `MauiSecretStore`, `MauiMainThread`, `ShellDialogService`, `ShellNavigationService`, ресурсы `Resources/` (стили, шрифты, иконки).
- Переезжают в Presentation: `PageModels/*` (кроме `ChatItemTemplateSelector`), `Routes.cs`, `Utilities/TaskExtensions.cs`, `Services/AppInitializer.cs`, `IDialogService`, `INavigationService`, `IMainThread`, `Resources/Strings/AppResources.resx` и `AppResources.cs`.
- Пространства имён меняются с `ImageGen.Maui.*` на `ImageGen.Presentation.*`. XAML MAUI ссылается на них (`xmlns:pm`, `x:DataType`, `x:Static strings:AppResources...`): эти ссылки правятся.
- `AppResources` читает ресурс по `LogicalName`; новое имя — `ImageGen.Presentation.Resources.Strings.AppResources.resources`.
- Пересечения: задачи 018, 019, 020, 027 в планах называют файлы `ImageGen/ImageGen.Maui/PageModels/...` — после 038 эти пути становятся `ImageGen/ImageGen.Presentation/PageModels/...` (комментарий в тех задачах). Делать 038 до них (очередь владельца).

## План
1. Проекты:
   - `ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj`: `net10.0`, `CommunityToolkit.Mvvm`, ссылки на Core и Data, `EmbeddedResource` для `AppResources.resx` с новым `LogicalName`, `InternalsVisibleTo` для тестов.
   - `ImageGen/ImageGen.Presentation.Tests/…csproj`: xUnit v3, NSubstitute, как Core.Tests (OutputType Exe), ссылка на Presentation.
   - `ImageGen.slnx` — оба проекта; `.github/workflows/build.yml`, задача `core` — format, build и test для обоих (по образцу Data и Data.Tests).
2. Перенос файлов из списка «Анализа» через `git mv` (история сохраняется), замена пространств имён `ImageGen.Maui.PageModels` → `ImageGen.Presentation.PageModels`, `ImageGen.Maui.Services` → `ImageGen.Presentation.Services` (для перенесённых интерфейсов), `ImageGen.Maui` → `ImageGen.Presentation` (для `Routes`), `ImageGen.Maui.Utilities` → `ImageGen.Presentation.Utilities`, `ImageGen.Maui.Resources.Strings` → `ImageGen.Presentation.Resources.Strings`.
3. MAUI: `ProjectReference` на Presentation; в XAML и C# обновить `xmlns`/`using`; убрать `EmbeddedResource` для `.resx`. Реализации (`MauiMainThread`, `ShellDialogService`, `ShellNavigationService`) реализуют интерфейсы из Presentation. Регистрации в `MauiProgram` не меняются по смыслу.
4. Тесты: `git mv` `ImageGen.Core.Tests/MauiLinked/*` и `ImageGen.Data.Tests/MauiLinked/*` в `ImageGen.Presentation.Tests/` (каталоги `PageModels/`, `Services/` и т. п. по типу), пространства имён по проекту. Тестам из Data.Tests нужна временная БД: перенести и `TestDatabase` (или ссылаться на общий тестовый помощник — выбрать при реализации, без дублирования логики). Из `Core.Tests` и `Data.Tests` убрать `Compile Link`, `EmbeddedResource` resx и `CommunityToolkit.Mvvm`.
5. Архитектурный тест `PresentationDependencyTests` по образцу `CoreDependencyTests`.
6. Документация: `docs/architecture.md` (раскладка: Presentation, Presentation.Tests; абзац про `MauiLinked` заменить на описание слоя представления), `docs/conventions.md` (правило «модели страниц в `ImageGen.Presentation`, без типов UI-фреймворков» вместо правила про `MauiLinked`), `docs/knowledge/dev-environment.md` (Presentation собирается везде).

### Тест-кейсы
- Все перенесённые тесты проходят без изменения утверждений.
- `PresentationDependencyTests`: сборка Presentation не ссылается на `Microsoft.Maui*`, `Avalonia*`, `Microsoft.AspNetCore*`.
- `AppResources`: каждый ключ читается из ресурса с новым именем (перенесённый тест `AppResourcesTests`).

### Команды проверки
```
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
scripts/check.sh
```
Windows-цель MAUI — CI (задача `maui`) или Windows-машина: `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0`.

Файлы: `ImageGen/ImageGen.Presentation/**` (новый), `ImageGen/ImageGen.Presentation.Tests/**` (новый), `ImageGen/ImageGen.Maui/**` (перенос и `using`/`xmlns`), `ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj`, `ImageGen/ImageGen.Core.Tests/MauiLinked/**` (перенос), `ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj`, `ImageGen/ImageGen.Data.Tests/MauiLinked/**` (перенос), `ImageGen/ImageGen.slnx`, `.github/workflows/build.yml`, `docs/architecture.md`, `docs/conventions.md`, `docs/knowledge/dev-environment.md`

## Отчёт

## Комментарии

### 2026-10-28, владелец (в чате, записал ревью)
План одобрен.
