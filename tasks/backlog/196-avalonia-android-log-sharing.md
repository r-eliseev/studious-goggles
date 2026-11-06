---
id: 196
status: planned
assignee:
claimed:
created: 2026-11-06
---

# Android-голова Avalonia: «Share logs»

## Описание
ADR 0026, задача 187 для второй оболочки. В MAUI кнопка «Share logs» отправляет копии логов через системное окно «Поделиться». В Android-голове Avalonia до этой задачи стоит `UnsupportedLogSharer` (195). Нужна реализация `ILogSharer` через Android `Intent` и `FileProvider`.

Зависит от: 195.
Среда: Linux с Android SDK (сборка); проверка на телефоне — 198.

## Критерии готовности
- `AndroidLogSharer : ILogSharer`, `IsSupported = true`; `ShareAsync(files)` открывает системный выбор приложения (`Intent.ActionSendMultiple`, тип `text/plain`, `ExtraStream` — список `content://`-адресов, флаг `GrantReadUriPermission`, `Intent.CreateChooser` с заголовком `AppResources.SettingsShareLogsTitle`).
- Адреса файлов — через `AndroidX.Core.Content.FileProvider.GetUriForFile` с authority `com.eliseev.imagegen.avalonia.fileprovider`; провайдер объявлен в `AndroidManifest.xml` (`exported=false`, `grantUriPermissions=true`) с `Resources/xml/file_paths.xml`, где разрешена только папка кэша (`<cache-path name="logs" path="imagegen-logs-share/" />` — именно туда копирует `SettingsPageModel`, 187).
- Регистрация в `MobileApp` вместо `UnsupportedLogSharer`; кнопка «Share logs» в Settings Android-головы видна (её показывает общий `SettingsView`, если `CanShareLogs`; если в `SettingsView.axaml` кнопки нет — добавить её так же, как в MAUI-странице: `ShareLogsCommand`, `IsVisible="{Binding CanShareLogs}"`, `AutomationProperties.Name`).
- Сборка Android-головы без предупреждений; Avalonia-тесты зелёные.

## Анализ
`SettingsPageModel.ShareLogsAsync` (задача 187) копирует логи в `Path.Combine(Path.GetTempPath(), "imagegen-logs-share")`. На .NET for Android `Path.GetTempPath()` — папка кэша приложения (`Context.CacheDir`); проверить в Отчёте фактическое значение через лог старта или явно записать, что не проверено на устройстве. Если `GetTempPath()` указывает не в `CacheDir`, `FileProvider` не отдаст файл — тогда в `file_paths.xml` указать фактический путь или остановиться и записать вопрос.

`Activity` для запуска — текущая `MainActivity` (статическое поле или `Platform.CurrentActivity`-аналог из `Avalonia.Android`; найти по пробнику/исходникам). Пакет `Xamarin.AndroidX.Core` уже приходит транзитивно с `Avalonia.Android`; если нет — добавить в `Directory.Packages.props`.

## План
1. `Resources/xml/file_paths.xml`, правка `AndroidManifest.xml` (провайдер).
2. `Platform/AndroidLogSharer.cs` (см. «Критерии»); `ShareAsync` вызывается из UI-потока (команда страницы), `StartActivity` на текущей активности.
3. `MobileApp`: зарегистрировать `AndroidLogSharer`.
4. Проверить кнопку в общем `SettingsView.axaml`.
5. Пункт для 198: Share logs → выбор приложения → файл пришёл и читается.

Файлы: `ImageGen/ImageGen.Avalonia.Android/Platform/AndroidLogSharer.cs` (новый), `ImageGen/ImageGen.Avalonia.Android/Resources/xml/file_paths.xml` (новый), `ImageGen/ImageGen.Avalonia.Android/AndroidManifest.xml` (новый или правка; найти, где пробник/шаблон держит манифест), `ImageGen/ImageGen.Avalonia.Android/MobileApp.axaml.cs`, при необходимости `ImageGen/ImageGen.Avalonia/Views/SettingsView.axaml`, `ImageGen/Directory.Packages.props`, `tasks/backlog/198-avalonia-android-manual-check.md`.

### Проверка
```
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
