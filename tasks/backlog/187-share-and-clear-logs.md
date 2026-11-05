---
id: 187
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-05T19:52:50Z
created: 2026-11-05
---

# Логи: поделиться файлами (Android) и очистить

## Описание
Запрос владельца 2026-11-05 после установки v0.1.7 на Android: индексирование падает («Indexing failed»), а логи посмотреть нельзя. Папка данных приложения (`FileSystem.AppDataDirectory`, подпапка `logs`) закрыта для других приложений; APK релизный (без `run-as`), `allowBackup=false`, Debug-провайдер логов пишет только при подключённом отладчике. Кнопка «Open logs» есть только на десктопе (`MauiFolderLauncher.IsSupported` — только `DeviceIdiom.Desktop`).

Нужно: на телефоне — кнопка «Share logs», открывающая системное окно «Поделиться» с файлами логов (Telegram, почта, «Сохранить в Загрузки»); на всех оболочках — кнопка «Clear logs».

Зависит от: —
Среда: Linux или Windows (логика и тесты — Presentation; MAUI собирается под Android на Linux). Проверка на телефоне — ручная, пункт в 140.

## Критерии готовности
- Settings → раздел логов: на телефонах (MAUI, не Desktop) видна кнопка «Share logs»; на десктопе (MAUI Windows, Avalonia) её нет, «Open logs» как раньше.
- «Share logs» отправляет копии всех файлов `imagegen-*.log` из папки логов одним системным окном «Поделиться». Копии делаются во временную папку, потому что текущий файл открыт Serilog на запись. Если файлов нет — сообщение «No log files yet», окно не открывается.
- «Clear logs» есть на всех оболочках. Спрашивает подтверждение; при согласии удаляет все файлы логов, кроме самого нового (в него сейчас пишет Serilog), и сообщает, сколько удалено. Отказ — ничего не удаляется.
- Ключи и пароли в логах не появляются (правило AGENTS.md уже действует; новых записей с секретами задача не добавляет).
- Тесты Presentation на выбор файлов, копирование открытого файла, очистку и команды страницы.

## Анализ
Логи: `ImageGenLogging` (`ImageGen/ImageGen.Presentation/Logging/ImageGenLogging.cs`), Serilog File sink, шаблон `imagegen-.log` → файлы `imagegen-yyyyMMdd.log` (и `imagegen-yyyyMMdd_001.log` при превышении 10 МБ), хранение 14 дней. Папка — `ImageGenLogging.LogDirectoryFor(dataDirectory)`; страница уже знает её как `SettingsPageModel.LogsFolder`.

Serilog держит текущий файл открытым на запись (чужим разрешено только чтение). Поэтому:
- читать текущий файл можно только через `FileStream` с `FileShare.ReadWrite`; `File.Copy` на Windows упадёт;
- удалять или обрезать текущий файл нельзя (Windows не даст удалить; на Android удаление «отвяжет» файл, и новые записи до следующего дня пропадут; обрезка оставит дыру из нулей). Отсюда правило «самый новый файл не удаляется». Самый новый = последний по имени при сортировке по возрастанию (имена содержат дату и суффикс `_NNN`).

Поделиться: MAUI Essentials `Share.Default.RequestAsync(new ShareMultipleFilesRequest { Title, Files })` с `ShareFile(path, "text/plain")`. FileProvider на Android Essentials подключает сам; правка `AndroidManifest.xml` не нужна (если сборка или запуск скажут иное — остановиться и записать в «Комментарии»). Временная папка: `Path.GetTempPath()` (на Android — папка кэша приложения) + подпапка `imagegen-logs-share`, очищается перед каждой отправкой.

Допущения (владелец может поменять): отправляются все файлы за 14 дней, а не только сегодняшний; очистка сохраняет текущий файл.

`SettingsPageModel` создаётся DI в `MauiProgram.cs` и `ImageGen.Avalonia/Composition/AppServices.cs`, в тестах — `CreateModel()` в `SettingsPageModelTests.cs`; новый параметр конструктора нужно добавить во все три места (и в `AppServicesTests`, если там собирается модель вручную).

## План
1. **`LogFiles`** — новый `static class` в `ImageGen/ImageGen.Presentation/Logging/LogFiles.cs`:
   - `IReadOnlyList<string> List(string logDirectory)` — полные пути файлов `imagegen-*.log`, по возрастанию имени (ordinal); нет папки → пустой список.
   - `int DeleteAllExceptNewest(string logDirectory)` — удаляет все из `List`, кроме последнего; файл, который не удалось удалить (`IOException`, `UnauthorizedAccessException`), пропускается; возвращает число удалённых.
   - `Task<IReadOnlyList<string>> CopyForSharingAsync(string logDirectory, string targetDirectory, CancellationToken)` — удаляет и заново создаёт `targetDirectory`, копирует каждый файл из `List` с тем же именем, открывая исходник `FileStream(..., FileMode.Open, FileAccess.Read, FileShare.ReadWrite)`; возвращает пути копий.
   Тесты (`ImageGen.Presentation.Tests/Logging/LogFilesTests.cs`, временная папка в `tmp`-подобном `Path.GetTempPath()` с уникальным именем, удаляется в `Dispose`):
   - три файла `imagegen-20261101.log`, `imagegen-20261102.log`, `imagegen-20261102_001.log` и посторонний `notes.txt` → `List` возвращает три лог-файла в этом порядке;
   - `DeleteAllExceptNewest` → удалены два первых, остались `imagegen-20261102_001.log` и `notes.txt`, результат 2;
   - пустая или отсутствующая папка → `List` пуст, `DeleteAllExceptNewest` = 0;
   - `CopyForSharingAsync`, пока самый новый файл открыт тестом `FileStream(FileMode.Append, FileAccess.Write, FileShare.Read)` (как у Serilog) → копии созданы, их содержимое равно исходному;
   - повторный `CopyForSharingAsync` после удаления одного исходника → в целевой папке нет старой копии.
2. **`ILogSharer`** — новый интерфейс в `ImageGen/ImageGen.Presentation/Services/ILogSharer.cs` по образцу `IFolderLauncher`: `bool IsSupported { get; }`, `Task ShareAsync(IReadOnlyList<string> files, CancellationToken)`. Плюс `UnsupportedLogSharer` (там же или отдельным файлом) с `IsSupported = false` и `ShareAsync`, который ничего не делает.
3. **Ресурсы** (`AppResources.resx` + свойства в `AppResources.cs`, по образцу `SettingsOpenLogs`): `SettingsShareLogs` = «Share logs», `SettingsShareLogsTitle` = «ImageGen logs», `SettingsClearLogs` = «Clear logs», `SettingsClearLogsConfirm` = «Delete old log files? The current file is kept.», `SettingsLogsCleared` = «Deleted {0} log files. The current file is kept.», `SettingsNoLogs` = «No log files yet.». Если в проекте есть тест полноты ресурсов (`AppResourcesTests`), он должен остаться зелёным.
4. **`SettingsPageModel`**: новый параметр конструктора `ILogSharer logSharer` (после `IFolderLauncher folders`).
   - `CanShareLogs => logSharer.IsSupported`.
   - `ShareLogsCommand`: файлы = `LogFiles.List(LogsFolder)`; пусто → `dialogs.AlertAsync(SettingsLogs, SettingsNoLogs)` и выход; иначе копии через `CopyForSharingAsync(LogsFolder, Path.Combine(Path.GetTempPath(), "imagegen-logs-share"), ...)` и `logSharer.ShareAsync(копии, ...)`. Ошибки — через существующий `_errorAlert`, как в других командах страницы.
   - `ClearLogsCommand`: `dialogs.ConfirmAsync(SettingsClearLogs, SettingsClearLogsConfirm, ...)` (кнопки — как в других подтверждениях страницы); отказ → выход; согласие → `LogFiles.DeleteAllExceptNewest(LogsFolder)` и `AlertAsync` с `SettingsLogsCleared` и числом.
   Тесты в `SettingsPageModelTests` (папка данных — временная, `IAppPaths` подменён; `CreateModel` получает `ILogSharer`-заглушку NSubstitute):
   - `CanShareLogs` = `IsSupported` заглушки (true/false);
   - нет файлов → показан `SettingsNoLogs`, `ShareAsync` не вызван;
   - два файла → `ShareAsync` получил два пути с теми же именами файлов вне папки логов;
   - очистка: отказ в подтверждении → файлы на месте; согласие → остался один самый новый, `AlertAsync` получил текст с «1».
5. **MAUI**: `ImageGen/ImageGen.Maui/Services/MauiLogSharer.cs` — `IsSupported => DeviceInfo.Idiom != DeviceIdiom.Desktop`; `ShareAsync` → `Share.Default.RequestAsync(new ShareMultipleFilesRequest { Title = AppResources.SettingsShareLogsTitle, Files = ... ShareFile(path, "text/plain") })`. Регистрация `AddSingleton<ILogSharer, MauiLogSharer>()` рядом с `IFolderLauncher` в `MauiProgram.cs`. В `SettingsPage.xaml` после кнопки «Open logs» две кнопки тем же стилем `ghost`: «Share logs» (`ShareLogsCommand`, `IsVisible="{Binding CanShareLogs}"`) и «Clear logs» (`ClearLogsCommand`), обе с `SemanticProperties.Description`.
6. **Avalonia**: регистрация `AddSingleton<ILogSharer>(new UnsupportedLogSharer())` в `AppServices.cs` рядом с `IFolderLauncher`; в `SettingsView.axaml` после «Open logs» кнопка «Clear logs» (`ClearLogsCommand`, `AutomationProperties.Name`). «Share logs» в Avalonia не добавлять.
7. **Документация**: `docs/knowledge/dev-environment.md`, раздел «Логи», пункт MAUI — как достать логи на Android (Settings → Share logs) и что релизный APK закрыт для `run-as`/backup. В `tasks/backlog/140-android-manual-check.md` дописать пункт: Settings → Share logs открывает окно «Поделиться», файл доходит в Telegram/«Загрузки» и читается; Clear logs удаляет старые файлы, текущий остаётся.

Файлы: `ImageGen/ImageGen.Presentation/Logging/LogFiles.cs` (новый), `ImageGen/ImageGen.Presentation/Services/ILogSharer.cs` (новый), `ImageGen/ImageGen.Presentation/PageModels/SettingsPageModel.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.cs`, `ImageGen/ImageGen.Presentation.Tests/Logging/LogFilesTests.cs` (новый), `ImageGen/ImageGen.Presentation.Tests/PageModels/SettingsPageModelTests.cs`, `ImageGen/ImageGen.Maui/Services/MauiLogSharer.cs` (новый), `ImageGen/ImageGen.Maui/MauiProgram.cs`, `ImageGen/ImageGen.Maui/Pages/SettingsPage.xaml`, `ImageGen/ImageGen.Avalonia/Composition/AppServices.cs`, `ImageGen/ImageGen.Avalonia/Views/SettingsView.axaml`, при необходимости `ImageGen/ImageGen.Avalonia.Tests/Composition/AppServicesTests.cs`, `docs/knowledge/dev-environment.md`, `tasks/backlog/140-android-manual-check.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
bash scripts/check.sh
```
Windows-цель MAUI проверяет CI.

### Порядок исполнения (для Luna)
- Сначала прочитай только файлы из строки «Файлы» и названные тесты; сигнатуры сверяй с текущим `main`.
- Шаги строго по порядку (ub-mode): падающий тест → запуск, убедиться, что он падает именно по описанной причине → минимальный фикс → тест зелёный.
- Не меняй файлы вне строки «Файлы», не переименовывай существующие члены сверх плана, не чини соседние находки — они в отдельных задачах.
- Если код расходится с планом (нет такого метода, другая сигнатура, тест уже есть), не импровизируй: остановись и запиши вопрос в «Комментарии».
- Команды `dotnet` выполняй по одной, не параллельно. После каждого изменённого проекта — `dotnet format <проект> --verify-no-changes`.
- В «Отчёте»: что изменено, команды и результаты, отличия от плана.

## Отчёт

## Комментарии

### 2026-11-05, claude-audit
Создано по просьбе владельца («заведи таску на шаренье логов», «и очистку»); план для Luna. Ждёт одобрения.

### 2026-11-05, владелец (в чате, записал claude-audit)
План одобрен: «убедись, что все таски распланированы для Луны» — «и заапрувь их».
