---
id: 106
status: approved
assignee:
claimed:
created: 2026-10-29
---

# Ошибка polling fal всегда завершает генерацию

## Описание
Найдено ревью 2026-10-29. После submit `ProviderJobPoller` повторяет только `ErrorKind.Network`. Остальные `ProviderException` выходят из iterator; `GenerationService.WatchQueuedAsync` их не ловит, background task faulted, generation остаётся `Queued`/`Running`. Handle удалённого provider также остаётся незавершённым.

Зависит от: —
Среда: Linux или Windows.

## Критерии готовности
- `ProviderException` именно из получения следующего poll update завершает generation: `Moderated` → `Filtered`, остальные kinds → `Failed`; kind/message и `CompletedAt` сохраняются, event публикуется один раз.
- Operational Warning/Error polling failure содержит только GenerationId, ProviderId, status и ErrorKind; ProviderException.Message и полный exception только отдельной Debug-записью. Prompt, response content, URL/query и key не попадают выше Debug.
- Неожиданное исключение из polling становится `Failed/Unknown` и не остаётся ненаблюдаемым.
- Ошибки `ApplyUpdateAsync`, file store и persistence не классифицируются как polling failure.
- Handle отсутствующего provider становится `Failed/ModelUnavailable`, без submit/poll.
- `RecoverInterruptedAsync` возвращает сумму missing-provider rows и строк, помеченных `Interrupted` bulk update.
- Network retry и запрет повторного submit сохраняются.

## Анализ
Текущий catch охватывает весь `await foreach`, поэтому ловит также ошибки обработки update. Нужен явный async enumerator: polling catch только вокруг `MoveNextAsync`, а `Current`/`ApplyUpdateAsync` вне него. `SaveAndNotifyAsync` пока подавляет repository error; это исправляет 113, поэтому 106 не тестирует propagation `UpdateGenerationAsync`.

Пересечения: 107, 108, 111 и 113 меняют `GenerationService`. Порядок общей цепочки: 106 → 107 → 109 → 124 → 108 → 121 → 111 → 113 → 122.

## План
Для Haiku/Luna: строго TDD, один сценарий за шаг. Не менять `ProviderJobPoller`: его network retry уже правилен. Если async-enumerator API не совпадает, записать compiler error, `blocked`, без альтернативного широкого catch.

Прочитай: `GenerationService.WatchQueuedAsync/RecoverInterruptedAsync`, `ProviderJobPoller`, `RunRepository` resumable methods, `GenerationServiceTests`.

1. Тест `PollAsync` бросает `ProviderException(InvalidKey)` → terminal Failed, исходные kind/message, `CompletedAt`, один persisted update/event, no submit.
2. Тест `PollAsync` бросает обычный exception → terminal Failed/Unknown, `WhenIdleAsync` не faulted.
3. Переписать watch на `GetAsyncEnumerator`/`MoveNextAsync`. Только `MoveNextAsync` внутри catches. `ProviderException` маппить по критериям; active job cancellation после 108 не маппить в Unknown. Operational log без exception object/provider message; detail отдельным LogDebug. ApplyUpdate/file/persistence этим catch/log не охватывать.
4. Boundary-тест: poll возвращает Succeeded, `IImageFileStore.SaveAsync` бросает. Ошибка не превращается в второй terminal polling status. До 113 `WhenIdleAsync` ожидаемо faulted; в 113 этот тест обновляется под supervisor log.
5. Test malformed persisted JobJson: JsonException → Failed/Unknown, CompletedAt, один persisted update/event, no submit/poll; operational log safe, exception detail only Debug.
6. Тест missing provider при recovery: `Failed/ModelUnavailable`, no submit/poll, update/event, count = missing count + bulk repository count.
7. Обновить XML-doc `RecoverInterruptedAsync` под count semantics и missing-provider behavior.

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
