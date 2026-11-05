---
id: 190
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-05T20:13:05Z
created: 2026-11-05
---

# Удалять осиротевшие временные файлы при запуске

## Описание
Вынесено из 177 (ревью 141 и 172). Атомарная запись создаёт временный файл рядом с целевым и переименовывает его. Если процесс упадёт между созданием и `File.Move`, временный файл остаётся навсегда:
- картинки и вложения: `images/<session>/.<имя>.<32 hex>.tmp` (`AtomicImageFileWriter`, имя скрытое);
- модель по умолчанию: `<папка данных>/defaults.json.<32 hex>.tmp` (`DefaultAssistantModelFile.SaveAsync`).

Зависит от: 177 (тот же `FileImageStore.cs`; делать после её слияния).
Среда: Linux или Windows.

## Критерии готовности
- При запуске (в `AppInitializer`, после инициализации базы) удаляются временные файлы этих двух шаблонов старше 1 часа.
- Свежие временные файлы (младше часа — запись может идти), чужие файлы, картинки и `defaults.json` не трогаются.
- Ошибка удаления одного файла — Warning (имя файла и тип исключения), обход продолжается, запуск не падает.
- Тесты Data и Presentation.

## Анализ
- Шаблон имени писателя — в `AtomicImageFileWriter` (`$".{Path.GetFileName(finalPath)}.{Guid.NewGuid():N}.tmp"`). Шаблон `defaults.json` — в `DefaultAssistantModelFile.SaveAsync` (`path + "." + Guid.NewGuid().ToString("N") + ".tmp"`).
- Проверка имени без регулярных выражений тоже подходит, но проще регулярное выражение: картинки `^\..+\.[0-9a-f]{32}\.tmp$`, defaults `^defaults\.json\.[0-9a-f]{32}\.tmp$`.
- Время файла — `File.GetLastWriteTimeUtc`; текущее время передаётся параметром `now` (в `AppInitializer` — из `TimeProvider`).
- `AppInitializer` (`ImageGen/ImageGen.Presentation/Services/AppInitializer.cs`) — первичный конструктор `(SqliteDatabase, GenerationService, ProviderRegistry)`; тесты — `ImageGen/ImageGen.Presentation.Tests/Services/AppInitializerTests.cs`. В Avalonia-тестах он создаётся в `ImageGen/ImageGen.Avalonia.Tests/TestDoubles/ShellHarness.cs` — проверить, вручную ли.

## План
1. Data, падающие тесты в `ImageGen/ImageGen.Data.Tests/FileImageStoreTests.cs`, метод `DeleteOrphanedTemporaryFiles(TimeSpan olderThan, DateTimeOffset now)` (временная папка данных; время файлов задаётся `File.SetLastWriteTimeUtc`):
   - `images/s1/.a.png.<32 hex>.tmp` двухчасовой → удалён;
   - такой же, но 10-минутный → остался;
   - `images/s1/a.png`, `images/s1/.a.png.tmp` (без hex), `images/s1/note.tmp` → остались;
   - `defaults.json.<32 hex>.tmp` двухчасовой в корне папки данных → удалён; `defaults.json` → остался;
   - нет папки `images` → без ошибки, результат 0.
   Метод возвращает число удалённых файлов.
2. Реализация в `FileImageStore`: обход `images/` (`Directory.EnumerateFiles(…, SearchOption.AllDirectories)`) по шаблону картинок и корня папки данных (`SearchOption.TopDirectoryOnly`) по шаблону defaults; удалять, если `now - lastWrite > olderThan`; на `IOException`/`UnauthorizedAccessException` — Warning и дальше. Синхронный метод (вызывается один раз при запуске, в фоне).
3. Presentation, падающий тест в `AppInitializerTests`: после `EnsureInitializedAsync` двухчасовой осиротевший файл в `images/` удалён. Как собрать `AppInitializer` с настоящим `FileImageStore` и временной папкой — по образцу существующих тестов файла.
4. `FileImageStore` зарегистрирован в DI только как `IImageFileStore` (`DataServiceCollectionExtensions.cs`), поэтому метод добавить в интерфейс `IImageFileStore` (`ImageGen/ImageGen.Core/Storage/IImageFileStore.cs`) с XML-комментарием «удаляет временные файлы атомарной записи, оставшиеся после сбоя». `AppInitializer` получает `IImageFileStore fileStore` и `TimeProvider timeProvider` (зарегистрирован в Core как `TimeProvider.System`) в первичный конструктор; в `InitializeAsync` после `database.InitializeAsync` — `await Task.Run(() => fileStore.DeleteOrphanedTemporaryFiles(TimeSpan.FromHours(1), timeProvider.GetUtcNow()))`. Тестовые реализации `IImageFileStore`, если есть (`rg -n "IImageFileStore" ImageGen --glob "*Tests*"`), получают метод, возвращающий 0.
5. Поправить все ручные создания `AppInitializer` в тестах (`rg -n "new AppInitializer" ImageGen`).

Файлы: `ImageGen/ImageGen.Data/FileImageStore.cs`, `ImageGen/ImageGen.Data.Tests/FileImageStoreTests.cs`, `ImageGen/ImageGen.Presentation/Services/AppInitializer.cs`, `ImageGen/ImageGen.Presentation.Tests/Services/AppInitializerTests.cs`, `ImageGen/ImageGen.Core/Storage/IImageFileStore.cs`, при необходимости `ImageGen/ImageGen.Avalonia.Tests/TestDoubles/ShellHarness.cs`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data/ImageGen.Data.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
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

### 2026-11-05, claude-audit
Вынесено из 177 при подготовке планов под Luna.

### 2026-11-05, владелец (в чате, записал claude-audit)
План одобрен: «убедись, что все таски распланированы для Луны» — «и заапрувь их».
