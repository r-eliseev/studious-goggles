---
id: 195
status: planned
assignee:
claimed:
created: 2026-11-06
---

# Android-голова Avalonia: проект, запуск, ключи

## Описание
ADR 0026. Продуктовый проект `ImageGen/ImageGen.Avalonia.Android` на основе пробника 094 (`poc/backlog/094-avalonia-mobile-research/ImageGen/ImageGen.Avalonia.Android/`). Приложение «Omnilite Avalonia» (`com.eliseev.imagegen.avalonia`) ставится рядом с MAUI-приложением и использует общие экраны и службы `ImageGen.Avalonia` (191–194). Отправка логов — задача 196, CI и релиз — 197.

Зависит от: 191, 192, 193, 194.
Среда: Linux с Android SDK и workload `android` (`dotnet workload install android`; как ставить SDK — `docs/knowledge/dev-environment.md`). Запуск на устройстве — ручная задача 198.

## Критерии готовности
- `dotnet build ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj -c Release` на Linux — 0 ошибок, 0 предупреждений; проект в `ImageGen/ImageGen.slnx`.
- Приложение при запуске открывает список сессий; навигация, экраны и модели страниц — общие (`AppServices.CreateShared`, 191).
- Платформенные службы: диалоги — `OverlayDialogService` (192), просмотр — `OverlayImageViewerService` (193), сравнение — `OverlayImageComparisonService` (194), буфер обмена, выбор файла, сохранение картинки — общие службы на `Func<TopLevel?>` (191), папка — «не поддерживается» (`IsSupported = false`), логи — `UnsupportedLogSharer` до задачи 196.
- Системный Back: закрывает верхний слой (диалог → просмотр → сравнение), иначе — шаг назад по навигации; на корневом экране не обрабатывается (Android сворачивает приложение).
- Клавиатура: открытая клавиатура не закрывает поле ввода (отступ снизу по `IInputPane`, как в пробнике).
- Ключи провайдеров — только в `AndroidSecretStore` (Android KeyStore, AES-256-GCM, файлы по `SecretFileNames`); запись атомарная (временный файл + `File.Move`); ошибка расшифровки — Warning без значения и имени секрета, результат `null`; никаких `catch` без типа.
- Папка данных — `Context.FilesDir`; файловый журнал как на десктопе (`AddImageGenFileLogging`, `LogLevelSettings`, `StartupLog` с оболочкой «Avalonia Android», `CrashLog`).
- Диагностические extras пробника (`probe-secret-*`) не переносятся.
- `docs/knowledge/avalonia-mobile.md` (новый), `docs/architecture.md` (раздел оболочек), `docs/knowledge/dev-environment.md` (сборка), `AGENTS.md` («Команды»: сборка Android-головы Avalonia), `docs/index.md` при добавлении файла.
- Папка пробника `poc/backlog/094-avalonia-mobile-research/` удалена (код перенесён; правила — `poc/README.md`).

## Анализ
Пробник (прочитать целиком, 521 строка): `ImageGen.Avalonia.Android.csproj` (ссылки на исходники десктопного проекта, `AssemblyName=ImageGen.Avalonia`), `MainApplication`, `MainActivity`, `MobileApp.axaml(.cs)`, `Views/MainView.axaml(.cs)` (страница, Back, `IInputPane`), `Views/MobileTweaks.cs` (клавиша Send), `Platform/AndroidAppPaths.cs`, `Platform/AndroidSecretStore.cs`, `Platform/TopLevelServices.cs` (заменяется общими службами 191–194), `Composition/MobileAppServices.cs` (заменяется `AppServices.CreateShared`), `Resources/`, `GlobalUsings.cs` (псевдонимы `Button`/`Application`). Пакеты пробника: `Avalonia.Android` 12.1.4, `Xamarin.AndroidX.Core.SplashScreen` 1.0.1.15 (`poc/.../Directory.Packages.props`) — добавить в `ImageGen/Directory.Packages.props`.

Десктоп для сравнения: `ImageGen/ImageGen.Avalonia/App.axaml(.cs)` — стили и запуск (логирование, `AvaloniaAppearanceApplier`, `navigation.AttachHost(PageArea)`, `GoToAsync(Routes.Sessions)`); `MobileApp` повторяет это без окна. Ресурсы и стили `App.axaml` (25 строк) переносятся в `MobileApp.axaml`.

Что подключать ссылками (без десктопных файлов): `Views/**` кроме `MainWindow.axaml(.cs)`; `Converters/**`; `Markup/**`; `Themes/**`; шрифты и лицензии `Assets/`; `Composition/AppServices.cs`; `Services/Navigation/**`; `Services/Logging/**`; `Services/Secrets/SecretFileNames.cs`; из `Services/`: `AvaloniaMainThread`, `AvaloniaAppearanceApplier`, `AvaloniaClipboardService`, `AvaloniaFilePickerService`, `AvaloniaImageSaver`, `DialogLayout`, `DialogButtonOrder` (если нужен), `OverlayDialogService`, `ImageViewerPanel`, `OverlayImageViewerService`, `ComparisonPanel`, `OverlayImageComparisonService`. Если при сборке общий файл тянет десктопный тип — не подключать вслепую соседние десктопные файлы: остановиться и описать в «Комментарии».

`TargetFramework` — `net10.0-android` (как MAUI); `SupportedOSPlatformVersion` — 23 (как в пробнике; у MAUI 21 — разница из-за Avalonia, записать в knowledge). Иконка: временно из пробника, отдельная задача оформления — не в этой задаче.

## План
1. Пакеты в `Directory.Packages.props`; проект `ImageGen.Avalonia.Android.csproj` (`OutputType=Exe`, `ApplicationId`, `ApplicationTitle=Omnilite Avalonia`, `ApplicationVersion=1`, `ApplicationDisplayVersion=1.0`, `AndroidPackageFormat=apk`, `AssemblyName=ImageGen.Avalonia`, `RootNamespace=ImageGen.Avalonia`, компилируемые привязки по умолчанию, ссылки на Core/Data/Presentation и файлы из «Анализа»). Добавить в `ImageGen.slnx`.
2. `MainApplication`, `MainActivity` (Label «Omnilite Avalonia», без диагностических extras), `Resources/` из пробника, `GlobalUsings.cs`.
3. `Platform/AndroidAppPaths.cs` (`FilesDir`), `Platform/AndroidSecretStore.cs` (по пробнику + атомарная запись и обработка ошибок из «Критериев»; `ISecretStore` и `ISecretPersistence`, `IsPersistentAsync` → `true`), `Platform/UnsupportedFolderLauncher.cs`.
4. `Views/MainView.axaml(.cs)`: `Grid` с `ContentControl PageHost` и поверх него `Grid OverlayHost`; статическое `MainView.Current`; при прикреплении к `TopLevel` — подписка на `BackRequested` и `InputPane` (по пробнику), отписка при откреплении. Back — порядок из «Критериев» через `TryCloseTop()` трёх overlay-служб, затем `AvaloniaNavigationService` (как в пробнике). Перенести `MobileTweaks` (Send).
5. `MobileApp.axaml(.cs)`: ресурсы как в `App.axaml`; запуск как в десктопном `App` без окна: `AppServices.CreateShared(paths, secrets, secrets, registerPlatform: RegisterAndroid, configureLogging: …file log…, debugTrace: …)`; `RegisterAndroid` регистрирует платформенные службы из «Критериев» с хостом `() => MainView.Current?.OverlayHost` и `TopLevel` `() => TopLevel.GetTopLevel(MainView.Current)`; `AttachHost(MainView.PageHost)` и `GoToAsync(Routes.Sessions)` — когда `MainView` создан (`MainViewFactory`).
6. Сборка Debug и Release на Linux; размер Release APK записать в `docs/knowledge/avalonia-mobile.md` рядом с MAUI.
7. Документация из «Критериев»; удалить папку пробника.

Файлы: `ImageGen/Directory.Packages.props`, `ImageGen/ImageGen.slnx`, `ImageGen/ImageGen.Avalonia.Android/**` (новый проект: `ImageGen.Avalonia.Android.csproj`, `MainApplication.cs`, `MainActivity.cs`, `MobileApp.axaml`, `MobileApp.axaml.cs`, `GlobalUsings.cs`, `Views/MainView.axaml`, `Views/MainView.axaml.cs`, `Views/MobileTweaks.cs`, `Platform/AndroidAppPaths.cs`, `Platform/AndroidSecretStore.cs`, `Platform/UnsupportedFolderLauncher.cs`, `Resources/**`, `Icon.png`), `docs/knowledge/avalonia-mobile.md` (новый), `docs/knowledge/dev-environment.md`, `docs/architecture.md`, `docs/index.md`, `AGENTS.md`, удаление `poc/backlog/094-avalonia-mobile-research/`.

### Проверка
```
dotnet workload install android
dotnet build ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj -c Debug
dotnet build ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj -c Release
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet format ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj --verify-no-changes
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

## Комментарии

### 2026-11-06, claude-audit
Создано по поручению владельца («распланируй андроид на авалонии, с билдами»; выбрано: второе приложение рядом с MAUI, APK в каждом релизе). ADR 0026. План для Luna, ждёт одобрения.
