---
id: 173
status: approved
assignee:
claimed:
created: 2026-10-31
---

# Avalonia-тесты на Windows: гонка удаления папки с фоновым чтением размеров картинки

## Описание
CI `windows-tests` на PR #61 (только документы и задачи, head `aa71699`, 2026-10-31) упал: `ChatKeyboardFlowTests.Enter_SendsInputAndAddsNoNewLine` — `IOException: The process cannot access the file '<id>.png' because it is being used by another process` в `TempAppPaths.Dispose` ← `ShellHarness.Dispose` (`ShellHarness.cs:110`). Тот же тест на предыдущем коммите прошёл: падение нестабильное, зависит от времени.

Зависит от: —
Среда: Linux или Windows (воспроизведение — Windows или CI `windows-tests`).

## Критерии готовности
- Причина подтверждена: какой код держит файл открытым в момент удаления папки.
- Тестовое окружение Avalonia дожидается всей фоновой работы с файлами данных теста, а не только генераций; тест не гасит ошибку удаления молча.
- `windows-tests` проходит стабильно (несколько прогонов подряд); на Linux поведение не меняется.

## Анализ
- `ShellHarness.Dispose` → `WaitForBackgroundGenerations` ждёт только `GenerationService.WhenIdleAsync()`, затем `Provider.Dispose()` и удаление папки.
- После успешной генерации `ChatPageModel` запускает фоновое чтение размеров картинки (задача 133): `FileImageStore.TryReadDimensionsAsync` открывает файл внутри `Task.Run` (ограничение в 2 одновременных чтения). Это чтение не входит в `WhenIdleAsync` и может держать PNG открытым, когда тест уже удаляет папку. На Linux открытый файл удаляется, на Windows — нет.
- `FilePathToBitmapConverter` читает файл целиком `File.ReadAllBytes` и сразу закрывает — менее вероятная причина, проверить.
- `docs/conventions.md`, «Тесты»: «Тесты, которые удаляют временную папку с базой, сначала дожидаются фоновой работы» — правило есть, но фоновое чтение размеров им не охвачено.

## План
Сначала воспроизведение, затем исправление (ub-mode).
1. Воспроизвести: тест в `Data.Tests` — запустить `TryReadDimensionsAsync` на медленном потоке (подменённое открытие или большой файл) и сразу удалить папку; на Windows ожидается `IOException`. Если на Linux не воспроизводится — проверка в CI `windows-tests`.
2. Исправление в продукте, а не только в тестах: `FileImageStore` отслеживает незавершённые фоновые чтения и даёт `WhenIdleAsync()` (по образцу `GenerationService.WhenIdleAsync`); `IImageFileStore` получает этот метод, если нужен оболочкам при закрытии; иначе — только в `FileImageStore` и тестовое окружение получает его из DI.
3. `ShellHarness.WaitForBackgroundGenerations` ждёт и `FileImageStore.WhenIdleAsync()`, с тем же сроком 10 с.
4. Не добавлять повтор удаления с подавлением ошибки: он скроет настоящие утечки дескрипторов.
5. Проверки: Data.Tests, Avalonia.Tests (Linux), CI `windows-tests` 3 прогона; `dotnet format`, `scripts/check.sh`.

Файлы: `ImageGen/ImageGen.Data/FileImageStore.cs`, `ImageGen/ImageGen.Data.Tests/FileImageStoreTests.cs`, `ImageGen/ImageGen.Avalonia.Tests/TestDoubles/ShellHarness.cs`; при необходимости `ImageGen/ImageGen.Core/Storage/IImageFileStore.cs` и `docs/conventions.md` («Тесты»).

## Отчёт

## Комментарии

### 2026-10-31, claude-audit
Создано при наблюдении за PR #61: падение не связано с изменениями PR (там только задачи и документы). Пересечение: 141 (атомарная запись) правит тот же `FileImageStore.cs` — делать после 141 или согласовать порядок при rebase.

### 2026-10-31, владелец (в чате, записал claude-audit)
План одобрен.
