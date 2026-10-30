---
id: 106
status: in-progress
assignee: opencode-vps
claimed: 2026-10-30T02:40:29Z
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

Нужность: устраняет зависание оплаченного задания и потерю восстановления; существующие тесты проверяют только успешный recovery.

Пересечения: 108, 111 и 113 меняют `GenerationService`; 107 меняет fal transport, но функционально ждёт terminal mapping из 106. После 106 ветки 107 → 109 → 124 и 108 можно выполнять независимо; lifecycle-цепочка: 108 → 126 → 121 → 111 → 113. 089 также меняет `CreateRunAsync`: при rebase сохранить её обновление сессии, не переписывать метод целиком.

## План
Для Haiku/Luna: строго TDD, один сценарий за шаг. Не менять `ProviderJobPoller`: его network retry уже правилен. Сверять async-enumerator API с целевой .NET проекта; рутинную ошибку компиляции исправлять локально. Не заменять узкую границу ошибок широким catch вокруг обработки update.

Прочитай: `GenerationService.WatchQueuedAsync/RecoverInterruptedAsync`, `ProviderJobPoller`, `RunRepository` resumable methods, `GenerationServiceTests`.

1. Параметризованный тест `WatchQueuedAsync_ProviderError_PersistsTerminalOnce`: `PollAsync` бросает `ProviderException(InvalidKey)` → terminal Failed; `ProviderException(Moderated)` → Filtered. В обоих случаях исходные kind/message, `CompletedAt`, один terminal persisted update/event, no submit. Настроить resumable generation и provider по существующему recovery-тесту, чтобы исключить начальные Queued/Running events.
2. Тест `PollAsync` бросает обычный exception → terminal Failed/Unknown, `WhenIdleAsync` не faulted.
3. Переписать watch на `GetAsyncEnumerator`/`MoveNextAsync`. Только `MoveNextAsync` внутри catches. `ProviderException` маппить по критериям; active job cancellation после 108 не маппить в Unknown. Operational log без exception object/provider message; detail отдельным LogDebug. ApplyUpdate/file/persistence этим catch/log не охватывать.
4. Boundary-тест: poll возвращает Succeeded, `IImageFileStore.SaveAsync` бросает. Ошибка не превращается в второй terminal polling status. До 113 `WhenIdleAsync` ожидаемо faulted; в 113 этот тест обновляется под supervisor log.
5. Test malformed persisted JobJson: JsonException → Failed/Unknown, CompletedAt, один persisted update/event, no submit/poll; operational log safe, exception detail only Debug.
6. Разделить два результата поиска: в `RecoverInterruptedAsync` непосредственно вызвать `providers.GetConnection`; только `ProviderException(ErrorKind.ModelUnavailable)` означает отсутствующую конфигурацию. Её generation → `Failed/ModelUnavailable`, `CompletedAt`, persisted update/event без submit/poll. Остальные provider exceptions не маскировать под missing config. Существующий connection без `IQueuedImageProvider` — другой случай: сохранить прежнее поведение (не запускать watch, не придумывать terminal status), подготовить отдельный regression test. `QueuedProviderOf` продолжает обслуживать обычный выбор пути; в recovery его null недостаточно для диагноза. Тест смешанного списка: missing-provider rows + bulk Interrupted count возвращаются суммой; resumed jobs в count не входят.
7. Обновить XML-doc `RecoverInterruptedAsync`, S3.8 в `docs/specs/chat-sessions.md` и «Первый запуск»/«Прогон» в `docs/architecture.md`: persisted handle возобновляется без submit; отсутствующий provider даёт ModelUnavailable; задания без resumable handle получают Interrupted; count равен реально завершённым recovery строкам. Обновление этих абзацев входит в 106, отдельная повторная правка в 119 не нужна.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`, `docs/architecture.md`, `docs/specs/chat-sessions.md`

После проверок заполнить «Отчёт» (изменения, проверки, непроверенное), выполнить `scripts/release-task.sh 106`, затем `bash scripts/check.sh`; код отправить PR по `docs/tasks.md`.

## Отчёт

## Комментарии
