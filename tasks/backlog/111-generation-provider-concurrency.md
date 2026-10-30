---
id: 111
status: in-progress
assignee: codex-gpt6
claimed: 2026-10-30T13:23:45Z
created: 2026-10-29
---

# Ограничить локально отслеживаемые jobs отдельно для каждого provider

## Описание
ADR 0003 требует per-provider semaphore. Сейчас один global slot pool, а queued slot освобождается после submit. Нужно не более четырёх локально отслеживаемых jobs provider от submit до terminal/local cancellation и независимость разных providers.

Зависит от: 121, 130.
Среда: Linux или Windows.

## Критерии готовности
- Provider id имеет собственный limit 4; provider A не блокирует B.
- Fresh normal/queued и recovered watch используют один общий helper acquisition.
- Queued slot удерживается submit→terminal либо до локальной cancellation.
- Отменённый WaitAsync не вызывает Release; acquired slot освобождается exactly once.
- Если remote cancel failed, удалённая операция может остаться неизвестной; лимит гарантирует только локально отслеживаемые jobs.
- OpenRouter adapter 102 не добавляет второй semaphore; ownership лимита остаётся GenerationService по superseding ADR задачи 130.
- `RunningJob.SubmitSettled` завершается exactly once при каждом выходе pre-submit/submit phase, включая cancellation во время ожидания slot; после успешного submit handle публикуется до signal.

## Анализ
После 108/121 единственный lifetime token — RunningJob CTS. Recovery сейчас обходит `_slots`. Semaphore dictionary живёт весь lifetime singleton и не dispose-ится per job.

Нужность: глобальные четыре slot сейчас блокируют независимого провайдера, а fal после submit вообще выходит из лимита. 111 вводит заявленный контракт лимита на provider.

Пересечения: 121 расширяет RunningJob/Stop (предпосылка), 113/125 меняют persistence того же lifecycle позже. Adapter102 можно писать параллельно: он не изменяет GenerationService и не имеет semaphore.

## План
Для Haiku/Luna: provider interfaces не менять. Tests синхронизировать gates/TCS, без timing sleeps.

1. Test normal same provider: 5 jobs, до gate ровно 4 entered; после release все terminal; max 4.
2. Test two providers: 4 blocked A не мешают B. Настроить router для обоих ids.
3. Queued test: 4 submitted и polling blocked; fifth SubmitAsync not called до terminal/local cancel.
4. Recovery test: 4 recovered handles занимают slots; fifth fresh/recovered ждёт.
5. Заменить `_slots` на private `_providerSlots: ConcurrentDictionary<string, SemaphoreSlim>` с StringComparer.Ordinal и значением SemaphoreSlim(4,4). `RunWithProviderSlotAsync(string providerId, CancellationToken ownedToken, Func<Task> work)` ждёт semaphore и выполняет work в try/finally только ПОСЛЕ успешного WaitAsync. Поставить этот helper единожды в общей launch/job-обёртке для normal Execute, fresh queued Execute и recovered Watch. Удалить внутренние захваты `_slots` в ExecuteAsync/ExecuteQueuedAsync: вложенный захват создаст deadlock. Recovery и fresh для одного id делят один semaphore.
6. Wait flag/acquired scope должен исключить Release после cancelled wait. Slot release в one finally вокруг полного lifecycle. На любом выходе slot-wait/submit phase завершить `RunningJob.SubmitSettled` exactly once; success сначала atomically publishes handle, потом signal. Recovery pre-completes signal after handle publication before launch.
7. Deterministic deletion test: 4 jobs hold slots, fifth waits; Stop fifth returns без открытия gate, SubmitAsync/CancelAsync не вызваны, unacquired semaphore не Release.
8. Own deletion cancellation 108 освобождает slot; failed remote cancel limitation остаётся в log/docs. После обычного terminal failure и persistence exception также ровно один release. Словарь/лимиты живут lifetime singleton; не удалять semaphore при завершении отдельного job, иначе одновременно возникнут два лимита на один provider.
9. В `docs/architecture.md` («Прогон») и S3.4 `docs/specs/chat-sessions.md` заменить прежний глобальный предел: не более4 локально отслеживаемых jobs НА provider id, включая свежий submit→terminal и recovered polling; один provider не блокирует другой; при неудаче remote cancel локальный лимит не обещает контроль оставшейся удалённой работы. Это обновление входит в111;120 повторно не выполняется.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`, `docs/architecture.md`, `docs/specs/chat-sessions.md`

После проверок заполнить «Отчёт» (изменения, проверки, непроверенное), выполнить `scripts/release-task.sh 111`, затем `bash scripts/check.sh`; отправить PR по `docs/tasks.md`.

## Отчёт

## Комментарии
