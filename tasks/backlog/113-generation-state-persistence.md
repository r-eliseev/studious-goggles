---
id: 113
status: approved
assignee:
claimed:
created: 2026-10-29
---

# Наблюдать сбой сохранения состояния generation

## Описание
`SaveAndNotifyAsync` подавляет любой repository exception. Кроме того, SQLite UPDATE нулевой строки считается success. Background task исчезает из `_running`, UI не получает event, DB/file могут разойтись. Эта задача вводит строгий persistence contract и supervisor. Retry и file compensation вынесены в 125/122.

Зависит от: 111.
Среда: Linux или Windows.

## Критерии готовности
- `RunRepository.UpdateGenerationAsync` требует affected rows = 1; zero/multiple rows → non-transient `StorageWriteException`.
- `SaveAndNotifyAsync` не подавляет failure и публикует event только после confirmed update.
- `Launch` наблюдает unexpected exception: operational Error содержит только безопасные ids/status, полный exception записывается только на Debug; Task завершается без fault. Own cancellation 108 не Error.
- Failure промежуточного Running status прекращает work до provider call и освобождает slot.
- `WhenIdleAsync` завершается без fault; no false terminal event.

## Анализ
IRunRepository сейчас возвращает Task, но Data implementation может проверить `ExecuteNonQueryAsync` и бросить. Новый Core `StorageWriteException` сохраняет interface и не ломает все substitutes. Поле `IsTransient` нужно следующей задаче 125; zero-row создаётся с false.

Нужность: неуспешный DB write сейчас выглядит для фоновой операции как успех; silent zero-row допускает потерю UI/DB consistency. Retry125 и compensation122 остаются отдельными маленькими шагами после строгого контракта.

Boundary file-save test из 106 после supervisor больше не ожидает fault и должен быть обновлён.

## План
Для Haiku/Luna: без retry, delays и file cleanup. Один строгий update + supervisor.

1. Добавить `StorageWriteException(string message, bool isTransient, Exception? innerException = null)` в Core Storage, readonly `IsTransient`. Контракт `IRunRepository.UpdateGenerationAsync` остаётся Task; в XML-doc описать подтверждённую запись одной строки либо exception. Zero-row задаёт false, не зависит от SqliteException и не требует нового Core→Data reference.
2. В Data.Tests/Repositories/RunRepositoryTests проверить настоящую temp SQLite: update existing row succeeds; random id throws StorageWriteException с IsTransient=false и безопасным generation id. Не конструировать невозможный SQL affected>1 через изменение запроса: guard проверяет count!=1, а schema primary key гарантирует максимум1. Тесты не проверяют UI event в Data.
3. В RunRepository проверить affected count exactly 1; иначе бросить safe exception с generation id.
4. Core test repository UpdateGenerationAsync бросает sentinel exception: no event; один safe supervisor Error содержит GenerationId/SessionId/ProviderId/Status без exception object/message. Отдельная Debug запись содержит exception detail по logging policy проекта; выше Debug нет sentinel prompt/key/URL. `WhenIdleAsync` завершается. Использовать CapturingLogger helper с structured state (после110 либо локально совместимо расширить его, не ломая старые Entries).
5. Удалить подавляющий catch из `SaveAndNotifyAsync`; repository update обязателен до LogStatus и event. LogStatus выполняется ровно один раз на подтверждённый transition, event only after success. Это место сохранит retry125 внутри update, не дублируя success log/event по числу attempts.
6. В `Launch` catch unexpected до finally/remove; expected own-token `OperationCanceledException` не Error. На Error логировать только безопасные ids/status без exception; полную exception detail — отдельной Debug записью. Не пытаться сохранять второй Failed status из supervisor.
7. Test Running update failure: provider not called, slot later доступен другой job, running entry removed.
8. Обновить boundary test 106: file Save exception observed supervisor, no second terminal persistence/event, WhenIdle no fault.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data/ImageGen.Data.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Storage/StorageWriteException.cs` (новый), `ImageGen/ImageGen.Core/Storage/IRunRepository.cs`, `ImageGen/ImageGen.Data/Repositories/RunRepository.cs`, `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Data.Tests/Repositories/RunRepositoryTests.cs`, `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`, `ImageGen/ImageGen.Core.Tests/TestDoubles/CapturingLogger.cs` (сохранить расширения110, если они уже в main)

После проверок заполнить «Отчёт» (изменения, проверки, непроверенное), выполнить `scripts/release-task.sh 113`, затем `bash scripts/check.sh`; отправить PR по `docs/tasks.md`.

## Отчёт

## Комментарии
