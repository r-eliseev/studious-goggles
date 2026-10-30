---
id: 121
status: done
assignee:
claimed:
created: 2026-10-29
---

# Отменять remote queued job при удалении сессии

## Описание
После локальной cancellation из 108 fal job может продолжать тарифицируемую работу. Есть race: stop видит job во время SubmitAsync без handle, отменяет token, а provider всё же возвращает handle позже. Нужна race-safe публикация handle и best-effort `CancelAsync` до удаления session data.

Зависит от: 124, 126.
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

Нужность: local CTS не отменяет уже оплаченную удалённую работу. Таймаут107/validation109 доступны транзитивно через124.

Пересекается с 111 по RunningJob и GenerationService; выполнить 121 до 111.

## План
Для Haiku/Luna: только queued remote cancel при deletion, без UI-кнопок задачи 024.

1. Расширить RunningJob private lock и полями `IQueuedImageProvider? QueuedProvider`, `ProviderJobHandle? SubmittedHandle`, `TaskCompletionSource SubmitSettled` с RunContinuationsAsynchronously. `PublishHandle` сохраняет provider+handle атомарно ровно один раз ДО проверки отмены owned token и ДО сохранения JobJson; затем завершает signal через TrySetResult. `CompleteSubmitPhase` в finally завершает signal при любом исходе (ошибка подготовки, missing provider, ожидание slot, exception/OCE submit), даже без handle. Normal job также завершает signal без handle. Recovery валидно десериализованный handle публикует до открытия launch gate; malformed JSON сохраняет поведение106 и signal без handle.
2. Race-test: SubmitAsync игнорирует initial cancellation до gate, stop begins, submit returns handle; stop обязательно вызывает CancelAsync exactly once before completion.
3. После ожидания start leases108 сначала снять snapshot ссылок RunningJob target session, затем отменить session StopSource. Snapshot обязателен ДО cancel: быстро завершающаяся task иначе исчезнет из `_running` вместе с handle. Для каждого snapshot job await SubmitSettled, взять опубликованную pair provider+handle; вызвать `CancelAsync(handle, CancellationToken.None)` (deadline107), затем дождаться всех local tasks. На cancel outcome не продолжать polling. Повторный Stop использует общий StopCompletion108 и не повторяет cancellation requests. Caller token Stop влияет лишь на ожидание, не на уже запущенную cleanup sequence.
4. Не вызывать cancel для normal provider/no handle; repeated Stop/Delete не вызывает remote cancel повторно.
5. Tests outcomes Cancelled/CancellationRequested/AlreadyCompleted/NotFound, thrown ProviderException и unexpected exception из CancelAsync: deletion продолжается. Operational Warning содержит ids/outcome без exception/message; отдельный Debug содержит detail; нигде выше Debug нет prompt/key/URL.
6. Recovery test с watcher, уже зарегистрированным ДО tombstone: сохранённый handle + concurrent delete → cancel once, после Stop complete no poll/write. Если tombstone уже стоит ДО recovery registration,108 пропускает watcher и remote cancel не обещает: данных RunningJob в этом процессе нет. Дополнительная гонка: submit возвращает handle после local cancellation; PublishHandle происходит до проверки token/persistence, Stop всё равно отправляет cancel. Если job уже terminal и удалена из `_running` ДО snapshot, отменять её не нужно.
7. Обновить XML-doc coordinator и раздел удаления сессии в `docs/architecture.md`: local writes гарантированно остановлены до DB/file delete; remote cancellation best-effort с timeout, удалённый job может продолжиться при failure. Не обещать отмену работы, которую процесс ещё не зарегистрировал.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core/Services/ISessionGenerationCoordinator.cs`, `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`, `docs/architecture.md`

После проверок заполнить «Отчёт» (изменения, проверки, непроверенное), выполнить `scripts/release-task.sh 121`, затем `bash scripts/check.sh`; отправить PR по `docs/tasks.md`.

## Отчёт

- `RunningJob` атомарно публикует queued provider/handle и сигнализирует завершение submit phase на всех путях. Recovery публикует сохранённый handle до запуска watcher.
- Session stop снимает snapshot активных jobs до cancellation, отменяет локальные tokens, ждёт submit phases, отправляет best-effort `CancelAsync` с независимым token, затем ждёт локальные задачи. Исключения remote cancel не мешают удалению; Warning содержит только ids/outcome, details идут отдельно в Debug.
- Обновлены контракт coordinator и описание удаления в architecture: локальные writes остановлены до удаления данных, remote cancel имеет best-effort гарантию и не охватывает незарегистрированные jobs.
- Проверки: Core tests 553/553; `dotnet format` Core и Core.Tests `--verify-no-changes --no-restore`; `bash scripts/check.sh` — все проверки пройдены.
- Отличие от плана: нет; проверены submit-after-cancellation race, все четыре CancelOutcome и ошибка remote cancel.

## Комментарии
