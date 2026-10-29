---
id: 122
status: approved
assignee:
claimed:
created: 2026-10-29
---

# Удалять image file, если terminal state не сохранился

## Описание
После provider success файл записывается до terminal DB update. Если все retries 125 исчерпаны, файл становится orphan, а DB остаётся Running. Нужна узкая compensation для конкретного только что сохранённого файла.

Зависит от: 129.
Среда: Linux или Windows.

## Критерии готовности
- Compensation только для нового image path этой generation и только после failed terminal Succeeded persistence.
- Единый queued path129 покрывает native fal и local adapter OpenRouter/OpenAI-compatible; normal branch уже удалена, заново её не вводить.
- `DeleteFileAsync` best-effort с cleanup token, не уже отменённым job token; после попытки in-memory ImagePath/MediaType очищены.
- Persistence exception остаётся основной для supervisor; cleanup exception логируется отдельно.
- При failure UPDATE нет Succeeded event и второго terminal update; session folder целиком не удаляется. Исключение обработчика события после successful UPDATE не запускает compensation и не удаляет подтверждённый файл.
- Task не считается завершённой до compensation.

## Анализ
Не компенсировать промежуточный Running/Failed status: файла там нет. Retry 125 выполняется до cleanup, чтобы не удалить результат после transient lock. FileImageStore.DeleteFileAsync уже идемпотентен для missing file.

Нужность: сохраняет согласованность DB/file при permanent storage failure после provider success. Retry125 входит транзитивно через129; только после исчерпания retries можно удалить новый файл.122 не нужна для Submit handle persistence: на этой фазе129 ещё не получает изображение.

## План
Для Haiku/Luna: не добавлять reconciliation scanner/outbox. Локальный path хранить рядом с operation, не искать файлы по каталогу.

1. Тест native queued success: `IImageFileStore.SaveAsync` возвращает новый path; terminal Succeeded UPDATE завершается permanent StorageWriteException → DeleteFileAsync(path) ровно1, ImagePath/MediaType очищены, no Succeeded event, supervisor113 получает исходный persistence exception. Такой же тест local adapter result подтверждает обе разновидности единого129 orchestration. Фазы контролировать TCS; initial Running/JobJson UPDATEs должны успешно пройти, ломается именно terminal Succeeded UPDATE.
2. Тесты retry границы: terminal UPDATE transient1/2, затем success →file сохранён, DeleteFileAsync не вызван, один Succeeded event; три transient failure →3 UPDATE attempts,2 fake delays125, затем1 cleanup; не повторять SaveAsync/provider submit. Permanent error →1 attempt и cleanup сразу. Own cancellation после получения path, но до подтверждённого UPDATE, тоже компенсирует файл и не даёт ложного event.
3. Извлечь из SaveAndNotifyAsync отдельные private этапы `PersistStateWithRetryAsync(Generation generation, CancellationToken ownedToken)` (только repository UPDATE+policy125) и `NotifyConfirmedState(Generation generation)` (LogStatus/event после успеха). Остальные transitions вызывают этапы последовательно, не меняя их порядок. `SaveResultAndPersistAsync(Generation generation, ImageGenerationResult result, CancellationToken ownedToken)` сохраняет файл, получает path и устанавливает ImagePath/MediaType/CostUsd/Status=Succeeded/CompletedAt; try/catch начинается сразу после получения path и охватывает ТОЛЬКО проверку owned token и PersistStateWithRetryAsync. При успехе вызвать NotifyConfirmedState СНАРУЖИ compensation catch. Нельзя окружать компенсацией весь SaveAndNotifyAsync: exception logger/subscriber после успешнойDB записи не означает несохранённый результат.
4. При exception/cancellation из terminal persistence выполнить DeleteFileAsync с конкретным новым local path и CancellationToken.None, дождаться попытки. В finally очистить in-memory ImagePath/MediaType даже при cleanup failure; не подменять исходную причину/не отправлять второй terminal UPDATE или event. Оригинальный exception rethrow с исходным stack. Cleanup failure отдельно safe Warning/Error с GenerationId/SessionId и известным внутренним относительным path, exception detail только Debug; supervisor логирует исходную persistence error, не cleanup error вместо неё. Task остаётся активной до окончания cleanup.
5. Boundary tests: exception подписчика после successful terminal UPDATE →подтверждённый file не удалён, UPDATE не повторён; промежуточный Running/JobJson/Failed update failure →нет cleanup (нового path нет); сам SaveAsync бросает до возврата path →этот helper не выдумывает path и не удаляет каталог/attachment. Гарантия122 ограничена файлом, путь которого SaveAsync успешно вернул; partial write внутри FileImageStore до exception этим механизмом не исправляется, это отдельная141 (planned, не предпосылка122). Не заявлять обратное.
6. Race deletion108/126/121: terminal UPDATE failure попадает на cleanup gate →DeleteAsync ещё не завершён; разрешить cleanup, затем job completes и session folder deletion. После Delete return ни старый job, ни compensation ничего не создают/не записывают. Native/local тесты только вCore с substitute store/repository; реальнаяDI/SQLite проверка остаётсяData.Tests/CompositionTests126, не добавлятьCore→Data reference.
7. Существующий `FileImageStoreTests.DeleteFileAsync_RemovesFileAndIgnoresMissing` уже проверяет нужную идемпотентность; запустить его в составеData.Tests, не писать дубликат и не менятьData код без новой необходимости. В отчёте указать ограничение шага5 и любые реально непроверенные ручные сценарии.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`. Существующий `ImageGen/ImageGen.Data.Tests/FileImageStoreTests.cs` только запускать, правка не нужна.

После проверок заполнить «Отчёт» (изменения, проверки, ограничения), выполнить `scripts/release-task.sh 122`, затем `bash scripts/check.sh`; отправить PR по `docs/tasks.md`.

## Отчёт

## Комментарии
