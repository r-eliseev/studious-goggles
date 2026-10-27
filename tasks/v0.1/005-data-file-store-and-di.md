---
id: 005
status: done
assignee:
claimed:
created: 2026-10-27
---

# Хранилище: файлы картинок и регистрация в DI

## Описание
Реализовать `IImageFileStore` (картинки в `images/<sessionId>/<generationId>.<ext>` внутри `IAppPaths.DataDirectory`) и метод `AddImageGenData()`, который регистрирует базу, репозитории и файловое хранилище как singleton.

Зависит от: 004.
Среда: Linux или Windows (нужен только .NET 10 SDK, MAUI-нагрузки не нужны).

## Критерии готовности
- `FileImageStore` реализует `IImageFileStore` с поведением из плана; `AddImageGenData()` регистрирует всё хранилище.
- Тест-кейсы из плана написаны и проходят вместе с остальными тестами `ImageGen.Data.Tests`.
- `dotnet format` для `ImageGen.Data` и `ImageGen.Data.Tests` проходит.

## Анализ
- В БД хранится путь относительно папки данных, поэтому смена папки не ломает записи.
- Расширение файла определяется по `media_type` ответа OpenRouter; неизвестный тип получает `.bin`.
- Пробная реализация: `poc/v0.1/005-data-file-store-and-di/` (подсказка, не копировать дословно, см. `poc/README.md`).

## План
1. `ImageGen.Data/FileImageStore.cs`: `sealed class FileImageStore(IAppPaths) : IImageFileStore`.
   - Константа `ImagesFolder = "images"`.
   - `SaveAsync(sessionId, generationId, content, mediaType, ct)`:
     ```
     проверить аргументы (content не null, mediaType не пустой)
     relative = images / sessionId("N") / generationId("N") + GetExtension(mediaType)
     создать каталог для GetFullPath(relative)
     записать байты
     вернуть relative
     ```
   - `GetFullPath(relative)` = `DataDirectory` + `relative`.
   - `DeleteSessionFilesAsync(sessionId, ct)`: удалить каталог `images/<sessionId>` рекурсивно, если он есть; если нет, ничего не делать.
   - `static GetExtension(mediaType)` без учёта регистра: `image/png` → `.png`, `image/jpeg` и `image/jpg` → `.jpg`, `image/webp` → `.webp`, `image/gif` → `.gif`, остальное → `.bin`.
2. `ImageGen.Data/DataServiceCollectionExtensions.cs`: `AddImageGenData(this IServiceCollection)` регистрирует singleton: `SqliteDatabase`, `ISessionRepository`, `IChatMessageRepository`, `IRunRepository`, `IImageModelRepository` (реализации из `ImageGen.Data.Repositories`), `IImageFileStore` → `FileImageStore`. `IAppPaths` регистрирует оболочка.
3. Тесты `ImageGen.Data.Tests/FileImageStoreTests.cs` (временный каталог в `Path.GetTempPath()`, удаляется в `Dispose`; `IAppPaths` через NSubstitute):
   - PNG сохраняется → возвращается `images/<s>/<g>.png`, по `GetFullPath` лежат те же байты.
   - `GetExtension`: `image/png` → `.png`, `image/jpeg` → `.jpg`, `IMAGE/WEBP` → `.webp`, `application/octet-stream` → `.bin`.
   - `DeleteSessionFilesAsync` после сохранения → файла больше нет.
   - `DeleteSessionFilesAsync` для сессии без файлов → без исключения.
4. Тест `ImageGen.Data.Tests/DataServiceCollectionExtensionsTests.cs`: с зарегистрированным `IAppPaths` после `AddImageGenData()` разрешаются `SqliteDatabase`, четыре репозитория и `IImageFileStore`.

Файлы: `ImageGen/ImageGen.Data/FileImageStore.cs`, `ImageGen/ImageGen.Data/DataServiceCollectionExtensions.cs`, `ImageGen/ImageGen.Data.Tests/FileImageStoreTests.cs`, `ImageGen/ImageGen.Data.Tests/DataServiceCollectionExtensionsTests.cs`

### Проверка
```
dotnet format ImageGen/ImageGen.Data/ImageGen.Data.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj --verify-no-changes
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
scripts/check.sh
```

## Отчёт
- Изменены и добавлены файлы: `ImageGen/ImageGen.Data/FileImageStore.cs` (`FileImageStore : IImageFileStore`, `GetExtension` внутренний статический), `ImageGen/ImageGen.Data/DataServiceCollectionExtensions.cs` (`AddImageGenData()`: singleton для `SqliteDatabase`, четырёх репозиториев и `IImageFileStore`), тесты `ImageGen/ImageGen.Data.Tests/FileImageStoreTests.cs` (8 кейсов с теорией) и `ImageGen/ImageGen.Data.Tests/DataServiceCollectionExtensionsTests.cs`.
- Относительный путь в БД собирается через `/` на любой ОС (`images/<sessionId:N>/<generationId:N>.<ext>`), полный путь — через `Path.Combine(DataDirectory, relative)`.
- `DeleteSessionFilesAsync` удаляет `images/<sessionId>` рекурсивно; если каталога нет, ничего не делает.
- Пакеты и `Directory.Packages.props` не менялись. `docs/` не менялись: раздела «Состояние кода» в `docs/architecture.md` уже нет (убран по `docs/log/2026-10-27-architecture-without-progress.md`), устройство совпадает с описанием.
- Проверка: `dotnet format --verify-no-changes` для `ImageGen.Data` и `ImageGen.Data.Tests` проходит (код возврата 0). `dotnet test --project ImageGen/ImageGen.Data.Tests` — 24 из 24 пройдены (16 старых и 8 новых).
- Отклонения от плана: нет по существу. Статус перед реализацией в файле был `planned`; одобрение дано в чате, поэтому статусы проставлены вручную, без `scripts/claim-task.sh` (он пушит в origin/main).

## Комментарии
