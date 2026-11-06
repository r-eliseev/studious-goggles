---
id: 191
status: done
assignee:
claimed:
created: 2026-11-06
---

# Avalonia: общая сборка контейнера и службы на `TopLevel`

## Описание
Первый шаг Android-головы Avalonia (ADR 0026). Сейчас `ImageGen/ImageGen.Avalonia/Composition/AppServices.cs` в одном методе `Create` регистрирует и общие службы, и десктопные (окна: `MainWindowOwner`). Android-голова подключит этот файл ссылкой, поэтому общую часть нужно отделить от десктопной, а службы, которым нужен только `TopLevel`, перевести с `Func<Window?>` на `Func<TopLevel?>`. Поведение десктопа не меняется.

Зависит от: —
Среда: Linux или Windows.

## Критерии готовности
- `AppServices.cs` содержит только общую часть: метод `CreateShared(...)`, который регистрирует всё, кроме платформенных служб, и принимает делегат `Action<IServiceCollection> registerPlatform`.
- Новый файл `AppServices.Desktop.cs` (`static partial class AppServices`) содержит прежний публичный `Create(...)` с той же сигнатурой и `MainWindowOwner`; он вызывает `CreateShared` и регистрирует десктопные службы. Вызовы `AppServices.Create` в `App.axaml.cs` и тестах не меняются.
- `AvaloniaClipboardService`, `AvaloniaFilePickerService`, `AvaloniaImageSaver`, `AvaloniaFolderLauncher` принимают `Func<TopLevel?>` вместо `Func<Window?>` и пользуются только API `TopLevel` (`Clipboard`, `StorageProvider`, `Launcher`).
- В общей части нет ни одного упоминания `Window`, `MainWindow`, `App` (десктопного класса), `DesktopSecretStore`.
- Все тесты Avalonia зелёные; новый тест композиции проверяет, что `CreateShared` с подставными платформенными службами собирается (`ValidateOnBuild`).

## Анализ
Платформенные службы, регистрируемые сейчас в `Create` через окно (их переносит `AppServices.Desktop.cs`): `IDialogService` (`AvaloniaDialogService` или параметр `dialogs`), `IFilePickerService`, `IClipboardService`, `IImageSaver`, `AvaloniaFullScreenController`, `IImageViewerService`, `IImageComparisonService`, `IFolderLauncher`, `ILogSharer`. Всё остальное в `Create` — общее (ядро, хранилище, `AppInitializer`, `IMainThread`, оформление, расходы, поиск, `ErrorAlert`, модель по умолчанию, `PageRoutes`, навигация, модели страниц, фабрики `Func<SessionModelsPageModel>`/`Func<SessionParametersPageModel>`, `Page<TModel>`). Параметры `openRouterHandler`, `configureLogging`, `debugTrace` относятся к общей части; `dialogs` — к десктопной.

`Func<Window?>` → `Func<TopLevel?>`: `Window` — наследник `TopLevel`, а `Func<out T>` ковариантен, поэтому десктоп передаёт тот же `MainWindowOwner`. Внутри служб заменить `TopLevel.GetTopLevel(owner())` на `owner()`, а `window.StorageProvider` — на `topLevel.StorageProvider`; если служба использует что-то только из `Window` (например, `Title`, `Owner`), остановиться и записать это в «Комментарии».

Тесты: `ImageGen/ImageGen.Avalonia.Tests/Composition/AppServicesTests.cs`, `Services/AvaloniaImageSaverTests.cs`, `TestDoubles/ShellHarness.cs` — должны собраться без правок или с минимальными (тип делегата).

## План
1. Падающий тест в `AppServicesTests`: `CreateShared_WithPlatformServicesFromTheShell_BuildsAndResolvesPages` — вызвать `AppServices.CreateShared(paths, secrets, persistence, registerPlatform: s => { регистрировать NSubstitute-заглушки девяти платформенных служб из «Анализа» }, ...)` и получить `SessionsPageModel`, `ChatPageModel`, `SettingsPageModel`, `PageRoutes`. Временная папка данных и секреты — как в существующих тестах этого файла. Заглушка `AvaloniaFullScreenController` не нужна, если `IImageViewerService` заглушен; убедиться по коду, что общая часть сама его не запрашивает.
2. Создать `Composition/AppServices.Desktop.cs`: `public static partial class AppServices` с прежним `Create(...)` (та же сигнатура и XML-комментарий) — вызывает `CreateShared(..., registerPlatform: services => RegisterDesktop(services, dialogs))`; `RegisterDesktop` регистрирует девять служб ровно как сейчас; сюда же перенести `MainWindowOwner`.
3. В `AppServices.cs`: класс сделать `static partial`, метод `Create` переименовать в `CreateShared` с параметром `Action<IServiceCollection> registerPlatform` (вызывается после общих регистраций, до `BuildServiceProvider`), удалить десктопные регистрации и `MainWindowOwner`, убрать параметр `dialogs` и ставшие лишними `using`.
4. Четыре службы перевести на `Func<TopLevel?>` (см. «Анализ»). Поправить их тесты, если они создают службу с `Func<Window?>`-лямбдой, требующей явного типа.
5. Убедиться: `rg -n "Window|MainWindow|DesktopSecretStore" ImageGen/ImageGen.Avalonia/Composition/AppServices.cs` пусто.

Файлы: `ImageGen/ImageGen.Avalonia/Composition/AppServices.cs`, `ImageGen/ImageGen.Avalonia/Composition/AppServices.Desktop.cs` (новый), `ImageGen/ImageGen.Avalonia/Services/AvaloniaClipboardService.cs`, `ImageGen/ImageGen.Avalonia/Services/AvaloniaFilePickerService.cs`, `ImageGen/ImageGen.Avalonia/Services/AvaloniaImageSaver.cs`, `ImageGen/ImageGen.Avalonia/Services/AvaloniaFolderLauncher.cs`, `ImageGen/ImageGen.Avalonia.Tests/Composition/AppServicesTests.cs`, при необходимости `ImageGen/ImageGen.Avalonia.Tests/Services/AvaloniaImageSaverTests.cs`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

### Порядок исполнения (для Luna)
- Сначала прочитай только файлы из строки «Файлы» и названные тесты; сигнатуры сверяй с текущим `main`.
- Шаги строго по порядку (ub-mode): падающий тест → запуск, убедиться, что он падает именно по описанной причине → минимальный фикс → тест зелёный.
- Не меняй файлы вне строки «Файлы», не переименовывай существующие члены сверх плана, не чини соседние находки — они в отдельных задачах.
- Если код расходится с планом (нет такого метода, другая сигнатура, тест уже есть), не импровизируй: остановись и запиши вопрос в «Комментарии».
- Команды `dotnet` выполняй по одной, не параллельно. После каждого изменённого проекта — `dotnet format <проект> --verify-no-changes`.
- В «Отчёте»: что изменено, команды и результаты, отличия от плана.

## Отчёт

- Выделена общая композиция `AppServices.CreateShared(..., registerPlatform, ...)`; десктопная `Create(...)` сохранила сигнатуру и поведение, регистрации оконных служб и `MainWindowOwner` находятся в `AppServices.Desktop.cs`.
- `AvaloniaClipboardService`, `AvaloniaFilePickerService`, `AvaloniaImageSaver` и `AvaloniaFolderLauncher` принимают `Func<TopLevel?>` и используют API `TopLevel` напрямую.
- Добавлен тест с подменами всех платформенных служб, `ValidateOnBuild` и проверкой разрешения основных страниц. TDD: до выделения `CreateShared` тест не компилировался.
- Проверки: Avalonia.Tests — 154 успешно, 2 пропущены (D-Bus/DPAPI недоступны в Linux); Avalonia desktop build — успешно, 0 предупреждений и ошибок; форматирование Avalonia и Avalonia.Tests прошло; `bash scripts/check.sh` прошло.
- `rg -n "Window|MainWindow|DesktopSecretStore" ImageGen/ImageGen.Avalonia/Composition/AppServices.cs` не находит совпадений. Ручной запуск Android-головы не относится к этой задаче.

## Комментарии

### 2026-11-06, claude-audit
Создано по поручению владельца («распланируй андроид на авалонии, с билдами»; выбрано: второе приложение рядом с MAUI, APK в каждом релизе). ADR 0026. План для Luna, ждёт одобрения.

### 2026-11-06, владелец (в чате, записал claude-audit)
План одобрен («Да» на вопрос об одобрении 191–198 и PR).
