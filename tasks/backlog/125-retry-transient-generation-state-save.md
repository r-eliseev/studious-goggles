---
id: 125
status: done
assignee:
claimed:
created: 2026-10-29
---

# Повторять transient сбой сохранения generation state

## Описание
После 113 SQLite busy/locked завершает job сразу. Нужны максимум три attempts только для transient StorageWriteException с deterministic delay. Permanent/zero-row не retry.

Зависит от: 113.
Среда: Linux или Windows.

## Критерии готовности
- Only IsTransient=true retries, max 3 total; permanent/cancellation/programming no retry.
- Фиксированный backoff100ms между attempts1→2 и2→3 через injectable delay; tests без real sleep.
- Success event/LogStatus once; safe Warning ids/attempt only, full exception Debug; final safe Error/detail Debug supervisor113.
- Data maps SQLite BUSY/LOCKED transient; zero-row permanent.

## Анализ
Core не знает SqliteException. Data internal pure code mapping. Чтобы не менять все GenerationService call sites, optional final constructor parameter `IAsyncDelay? delay = null`; private field uses injected service or `SystemAsyncDelay(timeProvider)`. DI registers service and injects it; existing manual constructors compile unchanged. Targeted tests pass `delay: fake`.

Нужность: transient BUSY/LOCKED — штатная конкуренция SQLite, повтор одной идемпотентной UPDATE безопаснее немедленного отказа. В125 не повторять submit/генерацию/file save, только UPDATE уже подготовленного generation state.

Пересечения:126/089 добавляют обязательные constructor arguments, сохранить их перед optional trace/delay. Retry располагается после113 и до единого queued пути129/compensation122.

## План
Для Haiku/Luna: no NuGet; no retry arbitrary Exception.

1. Добавить `IAsyncDelay.DelayAsync(TimeSpan delay, CancellationToken cancellationToken): Task` и `SystemAsyncDelay(TimeProvider timeProvider)` с `Task.Delay(delay, timeProvider, cancellationToken)`. Зарегистрировать единственный IAsyncDelay/SystemAsyncDelay singleton в Core DI. Не добавлять NuGet/package/time-provider test dependency: fake реализует интерфейс и фиксирует вызовы.
2. В GenerationService последним optional argument после существующих optional добавить `IAsyncDelay? delay = null`; private `_delay = delay ?? new SystemAsyncDelay(timeProvider)`. Не терять trace и обязательные параметры089/126. Целевые тесты передают именованный delay:fake; остальные new остаются совместимыми.
3. В RunRepository internal static `IsTransientWriteError(SqliteException exception)` возвращает true только при primary `SqliteErrorCode`5 (SQLITE_BUSY) или6 (SQLITE_LOCKED), включая extended codes с тем же primary. Catch только SqliteException вокруг полного тела UpdateGenerationAsync (OpenAsync/ExecuteNonQueryAsync); wrap в StorageWriteException с safe message/generation id и IsTransient из helper, исходный exception — inner. Все прочие SQLite codes →false; zero-row exception113 уже permanent и не перехватывается. OCE и не-SQLite exceptions не оборачивать. Internal helper доступен Data.Tests через существующий InternalsVisibleTo; проверять constructed SqliteException кодов5/6/19, без реальных lock timing.
4. В SaveAndNotifyAsync private constant `StateSaveMaxAttempts=3`, `StateSaveRetryDelay=100ms`. Для attempts1..3 вызывать только UpdateGenerationAsync с owned token. Catch только StorageWriteException(IsTransient=true) при attempt<3 и неотменённом token: безопасный Warning с GenerationId/SessionId/ProviderId/attempt, отдельный Debug exception, затем _delay.DelayAsync(100ms,ownedToken). Финальную/постоянную ошибку пробросить supervisor113; он делает единственный финальный Error. LogStatus и GenerationUpdated расположены после подтверждённого UPDATE вне retry loop, по одному разу.
5. Тесты gate/fake: success2 →2 update/1 delay; success3 →3 update/2 delay; три transient failure →3 update/2 delay/no event/1 supervisor Error; permanent zero-row →1 update/0 delay; arbitrary exception →1 update/0 delay; отмена до UPDATE или во время delay →никаких последующих attempts и no Error для owned cancellation. Тест successful retry проверяет один event/LogStatus и отсутствие повторного provider call/file save. Fake delay должен соблюдать cancellation, можно удерживать TCS для отмены в точной фазе.

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

Файлы: `ImageGen/ImageGen.Core/Services/IAsyncDelay.cs` (новый), `ImageGen/ImageGen.Core/Services/SystemAsyncDelay.cs` (новый), `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs`, `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Data/Repositories/RunRepository.cs`, `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`, `ImageGen/ImageGen.Data.Tests/Repositories/RunRepositoryTests.cs`

После проверок заполнить «Отчёт» (изменения, проверки, непроверенное), выполнить `scripts/release-task.sh 125`, затем `bash scripts/check.sh`; отправить PR по `docs/tasks.md`.

## Отчёт

- Добавлены `IAsyncDelay`/`SystemAsyncDelay`, singleton DI registration и injectable delay для `GenerationService`. State UPDATE повторяется максимум трижды только при `StorageWriteException.IsTransient`, с двумя 100 ms backoff максимум; status log/event остаются после подтверждённого update и выполняются один раз.
- Data repository оборачивает SQLite write errors в `StorageWriteException`; только primary SQLITE_BUSY/LOCKED (5/6, включая extended code) transient. Zero-row остаётся permanent.
- Тесты проверяют retry после одной/двух ошибок, исчерпание, permanent failure, отмену во время delay и SQLite-коды.
- Проверки: Core tests 563/563; Data tests 95/95; `dotnet format` Core, Data и обоих test projects `--verify-no-changes --no-restore`; `bash scripts/check.sh` — все проверки пройдены.
- Отличие от плана: нет.

## Комментарии
