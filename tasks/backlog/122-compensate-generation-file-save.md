---
id: 122
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Удалять image file, если terminal state не сохранился

## Описание
После provider success файл записывается до terminal DB update. Если все retries 125 исчерпаны, файл становится orphan, а DB остаётся Running. Нужна узкая compensation для конкретного только что сохранённого файла.

Зависит от: 125.
Среда: Linux или Windows.

## Критерии готовности
- Compensation только для нового image path этой generation и только после failed terminal Succeeded persistence.
- Normal и queued paths покрыты.
- `DeleteFileAsync` best-effort с cleanup token, не уже отменённым job token; после попытки in-memory ImagePath/MediaType очищены.
- Persistence exception остаётся основной для supervisor; cleanup exception логируется отдельно.
- Нет Succeeded event и второго terminal update; session folder целиком не удаляется.
- Task не считается завершённой до compensation.

## Анализ
Не компенсировать промежуточный Running/Failed status: файла там нет. Retry 125 выполняется до cleanup, чтобы не удалить результат после transient lock. FileImageStore.DeleteFileAsync уже идемпотентен для missing file.

## План
Для Haiku/Luna: не добавлять reconciliation scanner/outbox. Локальный path хранить рядом с operation, не искать файлы по каталогу.

1. Normal test: SaveAsync returns path, terminal update fails permanent after retry policy → DeleteFileAsync(path) once, clear fields, no event, supervisor logs persistence error.
2. Queued Succeeded test с тем же expected behavior.
3. Cleanup failure test: отдельный safe Error/Warning cleanup с path/id без exception detail; Debug содержит cleanup exception; supervisor сохраняет original persistence error.
4. Использовать единый private `SaveResultAndPersistAsync` для normal и queued paths: сохранить returned path локально, set fields, выполнить terminal persistence. Catch должен окружать только persistence call, но компенсировать любой его exception (включая cancellation), затем rethrow original; ошибки самого file Save до получения path не компенсировать.
5. Cleanup использует `CancellationToken.None`; не удалять session directory и attachment.
6. Race deletion 108: Stop ждёт task вместе с compensation, затем session file delete; test no recreated file.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`, `ImageGen/ImageGen.Data.Tests/FileImageStoreTests.cs` (idempotent cleanup regression if missing)

## Отчёт

## Комментарии
