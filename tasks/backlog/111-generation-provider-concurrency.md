---
id: 111
status: planned
assignee:
claimed:
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

## План
Для Haiku/Luna: provider interfaces не менять. Tests синхронизировать gates/TCS, без timing sleeps.

1. Test normal same provider: 5 jobs, до gate ровно 4 entered; после release все terminal; max 4.
2. Test two providers: 4 blocked A не мешают B. Настроить router для обоих ids.
3. Queued test: 4 submitted и polling blocked; fifth SubmitAsync not called до terminal/local cancel.
4. Recovery test: 4 recovered handles занимают slots; fifth fresh/recovered ждёт.
5. Добавить `ConcurrentDictionary<string, SemaphoreSlim>` с ordinal ids и private `RunWithProviderSlotAsync(providerId, ownedToken, work)`. Использовать для normal, queued и recovery.
6. Wait flag/acquired scope должен исключить Release после cancelled wait. Slot release в one finally вокруг полного lifecycle. На любом выходе slot-wait/submit phase завершить `RunningJob.SubmitSettled` exactly once; success сначала atomically publishes handle, потом signal. Recovery pre-completes signal after handle publication before launch.
7. Deterministic deletion test: 4 jobs hold slots, fifth waits; Stop fifth returns без открытия gate, SubmitAsync/CancelAsync не вызваны, unacquired semaphore не Release.
8. Own deletion cancellation 108 освобождает slot; failed remote cancel limitation остаётся в log/docs.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`

## Отчёт

## Комментарии
