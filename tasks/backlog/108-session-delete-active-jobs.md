---
id: 108
status: done
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
Нужность: без учёта стартующих run и принадлежащих сессии tasks удаление может завершиться раньше записи картинки/статуса. 108 вводит только Core lifecycle. Реальное удаление подключает 126; отмену оплаченного удалённого задания — 121.

`StartRunWithAttachmentsAsync` знает SessionId до первого await; `StartRunAsync` делегирует ему. `RepeatAsync`/`NewSeedAsync`/`EditAsync`/`RunOnModelAsync` получают SessionId из `GetSourceAsync` и сразу после этого берут start lease, до чтения параметров/создания run. Tombstone хранится до конца жизни singleton: Guid удалённой сессии не используется заново.

Пересечения: 089 меняет `CreateRunAsync` (сохранить touch/event после записи run); 106 меняет watcher, после неё 108 и transport-цепочка 107 → 109 → 124 независимы. Порядок lifecycle: 108 → 126 → 121. 111/113/129 сохраняют введённые здесь cancellation и registration contracts.

## План
Для Haiku/Luna: только GenerationService + interface + Core tests. Не менять SessionService/DI/Fal remote cancel. Ни один lock не удерживается через await; не добавлять polling/sleep для ожидания других tasks.

1. Добавить публичный интерфейс `ISessionGenerationCoordinator` с методом `Task StopSessionAsync(Guid sessionId, CancellationToken cancellationToken)` в `ImageGen.Core.Services`. `GenerationService` реализует интерфейс; DI-регистрация — 126. Старт после tombstone отклоняется `InvalidOperationException` с безопасным сообщением до `AddRunAsync` и обращения к provider.
2. Под одним private `_lifecycleGate` хранить `_sessionStates: Dictionary<Guid, SessionJobState>`. `SessionJobState`: `IsDeleting`, `StartLeaseCount`, `StartsDrained` (TaskCompletionSource с RunContinuationsAsynchronously, завершён при count=0), `StopSource` (session-owned CTS), `StopCompletion` (единственный результат stop). Private `AcquireStartLease(sessionId)` под lock проверяет IsDeleting, при переходе 0→1 создаёт новый StartsDrained, увеличивает count и возвращает IDisposable `StartLease`. Его Dispose под тем же lock уменьшает count и при нуле завершает signal ровно один раз. Для состояния без стартов использовать уже завершённый signal.
3. Взять lease в public входах по «Анализу» и освобождать в finally/using на всех исходах, включая no-model/no-reference/error/caller cancellation. `CreateRunAsync` принимает held lease (private contract): `AddRunAsync` и регистрация всех jobs происходят до release. Stop, начавшийся после допуска lease, ждёт его конца; уже допущенный start завершает подготовку/регистрацию, не делает повторный отказ после successful AddRun. Start, начавшийся после tombstone, не допускается.
4. Заменить значение `_running` на private `RunningJob` с `GenerationId`, `SessionId`, owned CTS и Task. CTS связывается с `SessionJobState.StopSource.Token`, НЕ с caller token подготовки. Сохранить launch gate: запись RunningJob регистрируется до открытия gate и выполнения work. Обычный Execute и recovery watch проходят через одну регистрацию. Owned token передать в ожидание semaphore, provider/poller, чтение references, file store и repository. `WhenIdleAsync` ждёт RunningJob.Task. Завершение удаляет job и освобождает owned CTS; session source нельзя dispose до завершения всех его jobs.
5. `StopSessionAsync`: под lock атомарно поставить IsDeleting и создать единственный StopCompletion; последующие stop возвращают ожидание того же task. Рабочая stop-процедура без caller token: дождаться StartsDrained, отменить session StopSource, снять snapshot зарегистрированных jobs этой сессии и дождаться их всех. Никакой новой регистрации после draining быть не может. Caller token применяется только к ожиданию StopCompletion (`WaitAsync`): отмена ожидания НЕ снимает tombstone и НЕ прекращает начатый stop. Ошибку job наблюдать; до 113 сохранять fault в результате stop/WhenIdle, не объявлять успешную остановку при ошибке. Сама отмена owned token не fault и не Error.
6. Во всех catch, которые сейчас перехватывают `Exception`, пропускать `OperationCanceledException` с отменённым owned token до внешней job-обёртки; только она завершает собственную отмену штатно. После await внешнего provider/IO проверять owned token до следующей записи/события: тестовый provider может проигнорировать cancellation. Stop не обещает отсутствия записи ДО своего завершения; он гарантирует отсутствие дальнейших writes/events ПОСЛЕ успешного завершения. Не менять сохранённые статусы на Cancelled в этой задаче: это не пользовательская отмена 024.
7. `RecoverInterruptedAsync` для каждого resumable row берёт такой же start lease до регистрации watcher; если сессия уже tombstoned — skip без poll/submit. Lock и регистрация совпадают с новым стартом; дублирующий вызов recovery не должен заменить уже запущенную job того же GenerationId. Сохранить missing-provider handling/count semantics из 106.
8. Детерминированные race-тесты через TaskCompletionSource (без sleeps): AddRun достиг gate → Stop ещё не завершён → разрешить AddRun → зарегистрированные jobs отменены/дожданы; tombstone раньше start → no AddRun/no provider; две сессии → останавливается только одна; отменён caller token после возврата StartRun → job продолжает работу; повторный/concurrent Stop → один stop; cancelled waiter → повторный Stop дожидается фактического окончания; recovery и stop на границе регистрации; provider игнорирует cancel → Stop ждёт его, затем no file/status/event; no-model/error подготовки освобождает lease. Каждый тест ждёт наблюдаемой фазы и проверяет только соответствующую сессию.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Services/ISessionGenerationCoordinator.cs` (новый), `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`

После проверок заполнить «Отчёт» (изменения, проверки, непроверенное), выполнить `scripts/release-task.sh 108`, затем `bash scripts/check.sh`; код отправить PR по `docs/tasks.md`.

## Отчёт

- Добавлен `ISessionGenerationCoordinator`; `GenerationService.StopSessionAsync` ставит постоянный tombstone, ждёт все допущенные start leases, отменяет session-owned jobs и ожидает их завершения. Caller cancellation отменяет только ожидание общего stop task.
- Все пути создания run (обычный старт, repeat/new seed/edit/other model) получают lease до подготовки и держат его через `AddRunAsync` и регистрацию jobs. Recovery также использует lease; tombstoned rows пропускаются, duplicate watcher registration не заменяет уже зарегистрированную job.
- Каждый `RunningJob` хранит session/generation ids, owned linked CTS и task. Caller preparation token не связывается с job. Cancellation проходит в semaphore, provider/poller, чтение вложений, image file store и repository; отмена session не записывает новый terminal status, а отменённые jobs не fault-ят `WhenIdleAsync`. Ошибки job остаются наблюдаемыми через `WhenIdleAsync`/stop.
- Добавлены детерминированные тесты на tombstone-before-start, preparation lease release, start-versus-stop на `AddRunAsync`, caller cancellation после старта, раздельную отмену двух сессий, независимое ожидание stop, provider/file no-write после stop, отменённого stop waiter, ошибку job, tombstoned recovery и duplicate recovery.
- Проверки: Core.Tests 414/414; Core build без предупреждений/ошибок; `dotnet format` Core и Core.Tests; `git diff --check`; `bash scripts/check.sh`.
- Подключение SessionService/DI и remote provider cancellation остаются в задачах 126 и 121 соответственно, как определено планом.

## Комментарии
