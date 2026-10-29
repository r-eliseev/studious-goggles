---
id: 102
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Общий in-memory queued adapter для synchronous image transport

## Описание
ADR 0005 требует представить OpenRouter как background job. OpenAI-compatible transport также synchronous; чтобы позже дать GenerationService один queued path, нужен общий in-memory adapter вокруг `IImageGenerator`. Эта задача создаёт только adapter/state machine и tests. ProviderRouter integration — 128, orchestration switch — 129.

Зависит от: 122, 130.
Среда: Linux или Windows.

## Критерии готовности
- Adapter implements IQueuedImageProvider поверх IImageGenerator для любого provider id; own HTTP CTS, observed task, thread-safe state.
- Submit регистрирует job до task start и сразу возвращает serializable local handle; второго concurrency semaphore нет (owner — GenerationService 111).
- Poll states Queued/Running/terminal; ProviderException maps kind, Moderated→Filtered; unexpected→Failed/Unknown; task never unobserved fault.
- Cancel idempotent и отменяет transport CTS; точные CancelOutcome tests.
- Handle неизвестного нового process adapter → terminal Failed/Interrupted без нового paid submit.
- Terminal jobs сохраняются 10 минут (`TerminalRetention`), затем удаляются sweep при Submit/Poll/Cancel; deterministic TimeProvider tests, no timer sleeps.

## Анализ
ADR 0019 (задача 130) заменяет несовместимые детали ADR0005 и фиксирует один вариант: общий adapter OpenRouter/OpenAI-compatible, semaphore только GenerationService, без queue-position обещания, terminal TTL 10 минут. Эта задача реализует state machine; normal orchestration path удалит 129. Adapter не занимается catalog/chat, persistence/files или provider concurrency. ProviderJobHandle использует ProviderId+RequestId; URL fields получают безопасные local `memory://` values и никогда не идут в HTTP/FalUrlValidator.

## План
Для Haiku/Luna: pure Core class + tests; ProviderRouter/GenerationService не менять.

1. Создать `InMemoryQueuedImageProvider(IImageGenerator transport, string providerId, TimeProvider)`. Internal JobEntry: state/result/error, own CTS, observed Task, timestamps.
2. Submit: validate request; create GUID request id/local handle; add entry; start task only after registration gate; return handle without waiting transport.
3. Task state machine: Running; await GenerateAsync own token; Succeeded result; ProviderException mapping; own OCE Cancelled; unexpected Failed/Unknown. Catch all inside task.
4. Poll validate ProviderId/local handle/request id. Existing returns snapshot. Unknown id/new adapter returns Failed/Interrupted. Terminal result remains available until TTL.
5. Cancel atomic/idempotent: unknown → NotFound; terminal → AlreadyCompleted; active → cancel CTS и return CancellationRequested. Poll позже показывает Cancelled после observed OCE.
6. Sweep terminal entries старше `TerminalRetention = 10 minutes` на Submit/Poll/Cancel using TimeProvider; no fire-and-forget timer. Active jobs never removed by TTL.
7. Tests registration race, immediate handle, states, result, each exception mapping, cancellation race/repeat, unknown after new instance, TTL/sweep, no internal concurrency cap.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Providers/InMemoryQueuedImageProvider.cs` (новый), `ImageGen/ImageGen.Core.Tests/Providers/InMemoryQueuedImageProviderTests.cs` (новый)

## Отчёт

## Комментарии
