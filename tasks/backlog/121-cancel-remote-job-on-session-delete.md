---
id: 121
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Отменять remote queued job при удалении сессии

## Описание
После локальной cancellation из 108 fal job может продолжать тарифицируемую работу. Есть race: stop видит job во время SubmitAsync без handle, отменяет token, а provider всё же возвращает handle позже. Нужна race-safe публикация handle и best-effort `CancelAsync` до удаления session data.

Зависит от: 107, 109, 124, 126.
Среда: Linux или Windows.

## Критерии готовности
- Fresh queued job публикует handle в RunningJob до/вместе с сохранением `JobJson`; recovery job получает handle до watch.
- Stop отменяет local token, ждёт завершения submit phase, затем вызывает remote cancel, если handle появился.
- Cancel использует отдельный неотменённый token; provider timeout 107 ограничивает ожидание.
- Invalid persisted handle отклоняется validation 109 до secret/HTTP.
- Любой `CancelOutcome` логируется operational Warning только с безопасными ids/outcome; exception object/message и provider details — отдельной Debug-записью. Remote failure не блокирует DB/file deletion.
- Гарантия сформулирована честно: после Delete нет local writes; при failed remote cancel операция provider может продолжаться.

## Анализ
Простой snapshot handle до cancellation теряет handle, возвращённый одновременно. RunningJob из 108 нужно расширить completion signal `SubmitSettled` и atomic handle publication. Нельзя использовать уже отменённый job CTS для CancelAsync.

Пересекается с 111 по RunningJob и GenerationService; выполнить 121 до 111.

## План
Для Haiku/Luna: только queued remote cancel при deletion, без UI-кнопок задачи 024.

1. Расширить RunningJob thread-safe handle state: publish once; `SubmitSettled` завершается и при success, и при exception/cancellation. Recovery публикует deserialized handle до launch.
2. Race-test: SubmitAsync игнорирует initial cancellation до gate, stop begins, submit returns handle; stop обязательно вызывает CancelAsync exactly once before completion.
3. Stop sequence для each queued job: cancel local CTS; await submit settled; if valid handle then `CancelAsync(handle, CancellationToken.None)` (его own Config.Timeout из 107 ограничивает call); затем await local Task.
4. Не вызывать cancel для normal provider/no handle; repeated Stop/Delete не вызывает remote cancel повторно.
5. Tests outcomes Cancelled/CancellationRequested/AlreadyCompleted/NotFound и thrown ProviderException: deletion продолжается. Operational Warning содержит ids/outcome без exception/message; отдельный Debug содержит detail; нигде выше Debug нет prompt/key/URL.
6. Recovery test: saved handle + concurrent delete → no poll after tombstone, cancel called once.
7. Обновить XML-doc coordinator с best-effort limitation.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core/Services/ISessionGenerationCoordinator.cs`, `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`

## Отчёт

## Комментарии
