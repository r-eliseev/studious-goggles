---
id: 125
status: approved
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
- Short fixed backoff through injectable delay; tests no real sleep.
- Success event/LogStatus once; safe Warning ids/attempt only, full exception Debug; final safe Error/detail Debug supervisor113.
- Data maps SQLite BUSY/LOCKED transient; zero-row permanent.

## Анализ
Core не знает SqliteException. Data internal pure code mapping. Чтобы не менять все GenerationService call sites, optional final constructor parameter `IAsyncDelay? delay = null`; private field uses injected service or `SystemAsyncDelay(timeProvider)`. DI registers service and injects it; existing manual constructors compile unchanged. Targeted tests pass `delay: fake`.

## План
Для Haiku/Luna: no NuGet; no retry arbitrary Exception.

1. IAsyncDelay + SystemAsyncDelay using `Task.Delay(interval, timeProvider, token)`. Register singleton.
2. GenerationService final optional parameter `IAsyncDelay? delay = null`, private fallback. Targeted tests explicit fake; unrelated constructors unchanged.
3. RunRepository internal `IsTransientWriteError(SqliteException)` native BUSY/LOCKED; wrap only these as transient StorageWriteException. Unit tests constructed SqliteException BUSY/LOCKED/nontransient; no lock timing integration.
4. Retry SaveAndNotify persistence total3, delay only between. LogStatus outside loop; intermediate safe Warning + Debug detail.
5. Tests second/third success counts/delays/one event; 3 failures supervisor; permanent one attempt; own cancellation during delay no Error.

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

## Отчёт

## Комментарии
