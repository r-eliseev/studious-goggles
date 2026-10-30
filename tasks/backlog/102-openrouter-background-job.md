---
id: 102
status: done
assignee:
claimed:
created: 2026-10-29
---

# Общий in-memory queued adapter для synchronous image transport

## Описание
ADR 0005 требует представить OpenRouter как background job. OpenAI-compatible transport также synchronous; чтобы позже дать GenerationService один queued path, нужен общий in-memory adapter вокруг `IImageGenerator`. Эта задача создаёт только adapter/state machine и tests. ProviderRouter integration — 128, orchestration switch — 129.

Зависит от: 130.
Среда: Linux или Windows.

## Критерии готовности
- Adapter implements IQueuedImageProvider поверх IImageGenerator для любого provider id; own HTTP CTS, observed task, thread-safe state.
- Submit регистрирует job до task start и сразу возвращает serializable local handle; второго concurrency semaphore нет (owner — GenerationService 111).
- Poll states Queued/Running/terminal; ProviderException maps kind, Moderated→Filtered; unexpected→Failed/Unknown; task never unobserved fault.
- Cancel idempotent и отменяет transport CTS; точные CancelOutcome tests.
- Handle неизвестного нового process adapter → terminal Failed/Interrupted без нового paid submit.
- Terminal jobs сохраняются 10 минут (`TerminalRetention`), затем удаляются sweep при Submit/Poll/Cancel; deterministic TimeProvider tests, no timer sleeps.

## Анализ
Принятый [ADR 0020](../../docs/decisions/0020-common-in-memory-queued-adapter.md) заменяет несовместимые детали ADR0005 и фиксирует один вариант: общий adapter OpenRouter/OpenAI-compatible, semaphore только GenerationService, без queue-position обещания, terminal TTL 10 минут. Эта задача реализует state machine; normal orchestration path удалит 129. Adapter не занимается catalog/chat, persistence/files или provider concurrency. ProviderJobHandle использует ProviderId+RequestId; URL fields получают безопасные local `memory://` values и никогда не идут в HTTP/FalUrlValidator.

Нужность: общий adapter устраняет дублирование фонового выполнения OpenRouter и OpenAI-compatible, сохраняя один orchestration path. 122 не предпосылка: adapter не сохраняет файлы/БД; его файлы не пересекаются с 111–125. После 130 можно выполнять независимо от lifecycle-цепочки GenerationService.

## План
Для Haiku/Luna: pure Core class + tests; ProviderRouter/GenerationService не менять.

1. Создать `InMemoryQueuedImageProvider(IImageGenerator transport, string providerId, TimeProvider timeProvider) : IQueuedImageProvider`. Private `_jobs` keyed RequestId, private `JobEntry` с Generation-independent данными: immutable handle, `JobUpdate Snapshot`, `CancellationTokenSource TransportSource`, `Task Work`, nullable `CompletedAt`. Доступ к snapshot/terminal transition синхронизировать одним lock per entry. Не добавлять Generation/SessionId/БД/provider semaphore. В 129 тип IImageGenerator переименуют; здесь использовать текущий контракт.
2. `SubmitAsync`: проверить request и caller cancellation ДО регистрации; create GUID RequestId (N format), handle с Config providerId и тремя `memory://<providerId>/<requestId>/{status|result|cancel}` URL. Зарегистрировать entry до открытия TaskCompletionSource launch gate; transport запускать на Task.Run без caller token планирования и без синхронного ожидания GenerateAsync. Caller token отвечает только за Submit preparation: его отмена после возврата handle не отменяет транспорт. Все worker exceptions наблюдаются внутри worker; Task никогда не остаётся faulted/unobserved.
3. Worker: Queued → Running → один terminal snapshot. Await transport.GenerateAsync с TransportSource.Token. Success → Succeeded с Result (включая MediaType/CostUsd); ProviderException(Moderated) → Filtered, остальные ProviderException → Failed с исходным ErrorKind/message; OCE при отменённом own CTS → Cancelled; посторонний OCE/обычный exception → Failed/Unknown. После await проверить own cancellation до публикации Succeeded: игнорирующий token transport не возвращает success после принятой отмены. Terminal snapshot и CompletedAt публикуются атомарно; поздняя Cancel не меняет уже terminal result.
4. `PollAsync` проверяет caller token и handle.ProviderId, Guid RequestId и точное совпадение local URL fields со схемой шага2. Wrong provider/malformed handle → безопасный ProviderException(Unknown), no transport access. Для валидного неизвестного RequestId (в том числе после restart/TTL) вернуть terminal Failed/Interrupted без submit. Для существующего вернуть согласованный immutable snapshot; Poll никогда не запускает transport заново.
5. `CancelAsync` валидирует handle/caller token так же. Валидный unknown → NotFound; terminal → AlreadyCompleted; active → отметить cancellation request, отменить TransportSource и вернуть CancellationRequested. Повторная active Cancel возвращает тот же CancellationRequested, transport cancellation не выполняется второй раз. Worker после завершения выдаёт Cancelled даже если transport проигнорировал cancellation. Cancel и TTL sweep не должны бросать ObjectDisposedException из гонки CTS; CTS освобождается только безопасно после worker completion/removal.
6. Sweep terminal entries с возрастом >= `TerminalRetention = 10 minutes` на Submit/Poll/Cancel using TimeProvider; no fire-and-forget timer. Active jobs never removed by TTL.
7. Детерминированные tests с TCS gates и ручным TimeProvider: Submit возвращает handle до открытия transport gate; registration видна при немедленном Poll; Running/terminal/result/cost; каждый mapping шага3; caller cancellation после Submit не останавливает transport; Cancel active/repeat/terminal/unknown; cancel против late success; wrong-provider/malformed handle; новый adapter с сохранённым handle → Interrupted без GenerateAsync; TTL exact boundary и после10мин; активные jobs sweep не удаляет; пять одновременно начатых transport calls не блокируются собственным лимитом adapter. Не писать timing sleeps.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Providers/InMemoryQueuedImageProvider.cs` (новый), `ImageGen/ImageGen.Core.Tests/Providers/InMemoryQueuedImageProviderTests.cs` (новый)

После проверок заполнить «Отчёт» (изменения, проверки, непроверенное), выполнить `scripts/release-task.sh 102`, затем `bash scripts/check.sh`; отправить PR по `docs/tasks.md`.

## Отчёт

- Добавлен `InMemoryQueuedImageProvider` с локальными `memory://` handles, немедленной регистрацией jobs, отдельным CTS транспорта, атомарными snapshot-ами и наблюдаемой фоновой работой. Роутер и `GenerationService` не менялись.
- Реализованы переходы Queued/Running/terminal, маппинг `ProviderException` и неожиданных ошибок, безопасная отмена с идемпотентным `CancellationRequested`, `Interrupted` для неизвестного process-local handle и строгая проверка provider id/RequestId/local URL fields.
- Terminal snapshots удерживаются 10 минут по `TimeProvider` и удаляются при Submit/Poll/Cancel; активные jobs не удаляются. Semaphore и queue position не добавлялись.
- Добавлены 23 теста: submit/registration, result/cost, error mapping, caller и transport cancellation, неизвестные/подменённые handles, restart, TTL boundary/sweep и пять параллельных transport jobs.
- Проверки: Core tests — 438/438; формат Core и Core.Tests; `scripts/check.sh`.
- Отклонений от плана нет.

## Комментарии
