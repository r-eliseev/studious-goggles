---
id: 108
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Координировать lifecycle generation jobs по сессии

## Описание
GenerationService не хранит SessionId/CTS у background tasks. Между `AddRunAsync` и `Launch` есть окно, где stop не увидит новый run. Эта задача вводит session tombstone/start leases, owned job tokens и `StopSessionAsync` внутри Core. Подключение к SessionService/DI — 126; remote cancel — 121.

Зависит от: 106.
Среда: Linux или Windows.

## Критерии готовности
- Stop атомарно ставит permanent tombstone; новые run/watch session после него не регистрируются.
- Start preparation либо полностью регистрирует все jobs под lease, либо отклоняется до AddRun; окна insert→launch нет.
- Jobs получают owned CTS отдельно от caller preparation token; stop отменяет/ожидает только target session.
- Recovery соблюдает tombstone.
- Own cancellation не fault-ит WhenIdle и не Error; jobs других sessions продолжаются.
- После Stop завершённая старая task не вызывает file/status writes.

## Анализ
StartRunWithAttachments знает SessionId до await; StartRun только делегирует. Repeat/NewSeed/Edit/RunOnModel узнают SessionId после GetSource и берут lease сразу затем. Tombstone живёт lifetime singleton. RunningJob сохраняет launch gate: dictionary registration до task start.

## План
Для Haiku/Luna: только GenerationService + interface + Core tests. Не менять SessionService/DI/Fal remote cancel.

1. Добавить `ISessionGenerationCoordinator.StopSessionAsync(Guid, CancellationToken)`; GenerationService implements, но DI пока не регистрировать.
2. Session state под lock: Deleting, active start leases, zero-leases signal. Lease placement по Analysis; private CreateRun требует held lease.
3. Race test gate AddRun: Stop ждёт registration; start после tombstone no AddRun/provider.
4. RunningJob immutable ids + owned CTS + Task; launch gate preserved. Caller token only preparation, owned token through semaphore/provider/poller/references/file/persistence.
5. Stop: tombstone, wait leases, cancel target jobs, await tasks. Repeated stop idempotent. OCE own CTS swallowed without Error; other exceptions сохраняют pre-113 behavior.
6. Recovery registration checks state under same lock; deleting session no watch.
7. Tests: two sessions; caller token cancelled after StartRun return does not stop job; recovery race; own cancellation; release provider/file gates after Stop and assert no subsequent file/status calls.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Services/ISessionGenerationCoordinator.cs` (новый), `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`

## Отчёт

## Комментарии
