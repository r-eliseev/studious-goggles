---
id: 106
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Ошибка polling fal всегда завершает генерацию

## Описание
Найдено ревью 2026-10-29. После успешного submit `ProviderJobPoller` повторяет только `ProviderException` с `ErrorKind.Network`. Ошибки `InvalidKey`, `RateLimited`, `BadRequest`, `ModelUnavailable` и другие выходят из итератора. `GenerationService.WatchQueuedAsync` их не ловит, фоновая задача faulted, а генерация остаётся `Queued` или `Running`. При следующем старте тот же сохранённый handle запускается снова. Задание с handle от удалённого из `providers.json` провайдера также остаётся незавершённым навсегда.

Зависит от: —
Среда: Linux или Windows.

## Критерии готовности
- Любая `ProviderException` из polling переводит генерацию в `Failed`, сохраняет исходный `ErrorKind` и сообщение, задаёт `CompletedAt` и публикует обновление.
- Неожиданное исключение polling переводит генерацию в `Failed/Unknown`; исключение не остаётся ненаблюдаемым.
- Задание с сохранённым handle, провайдер которого больше не настроен, становится `Failed/ModelUnavailable` при восстановлении.
- Сетевые ошибки по-прежнему повторяются по правилам `ProviderJobPoller`; submit платного задания не повторяется.
- Добавлены тесты всех трёх сценариев.

## Анализ
- `ProviderJobPoller.WatchAsync` ловит только `ErrorKind.Network`: `ImageGen/ImageGen.Core/Providers/ProviderJobPoller.cs`.
- `GenerationService.WatchQueuedAsync` ловит только `JsonException`, `InvalidOperationException` и `IOException`: `ImageGen/ImageGen.Core/Services/GenerationService.cs`.
- `RecoverInterruptedAsync` запускает только найденный `IQueuedImageProvider`, но `MarkUnfinishedAsInterruptedAsync` намеренно не трогает строки с `job_json`: `ImageGen/ImageGen.Data/Repositories/RunRepository.cs`.
- Задачи 107, 108, 111 и 113 также меняют `GenerationService`; выполнять их последовательно, не параллельно. Рекомендуемый порядок: 106 → 107 → 108 → 111 → 113.

## План
Для исполнителя (план рассчитан на Haiku/Luna): делай шаги строго по порядку и по TDD. Сначала добавляй один падающий тест, затем минимальную правку для него. Не меняй публичные контракты вне указанного. Если текущие типы отличаются от плана или выбранный `ErrorKind` отсутствует, запиши это в «Комментарии», поставь `blocked` и остановись.

Прочитай перед началом: `docs/conventions.md`, `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core/Providers/ProviderJobPoller.cs`, `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`.

1. В `GenerationServiceTests` добавить тест: восстановленное queued-задание, у которого `PollAsync` бросает `ProviderException(ErrorKind.InvalidKey, ...)`, после `WhenIdleAsync` имеет `Failed`, `InvalidKey`, сообщение и `CompletedAt`; `UpdateGenerationAsync` вызван, `SubmitAsync` не вызван.
2. В `WatchQueuedAsync` отдельно обработать `ProviderException`: записать warning без промпта/ключа, перенести kind/message в generation, завершить и сохранить её. Сетевые повторы остаются внутри `ProviderJobPoller`.
3. Добавить тест на обычное неожиданное исключение именно при получении следующего provider update: состояние `Failed`, kind `Unknown`, задача `WhenIdleAsync` не падает. Получать элементы async-enumerator так, чтобы catch охватывал только `MoveNextAsync`/`PollAsync`; `ApplyUpdateAsync`, сохранение файла и persistence выполнять вне этого catch. Ошибки `ApplyUpdateAsync` должны доходить до supervisor задачи 113, а не превращаться в polling failure. Не перехватывать отмену, если token отменён после задачи 108.
4. Добавить отдельный тест границы: `ApplyUpdateAsync` получает Succeeded, а `SaveAsync` или `UpdateGenerationAsync` бросает исключение → polling catch его не маскирует и не записывает второй terminal status.
5. Добавить тест восстановления handle с отсутствующим провайдером. В `RecoverInterruptedAsync` явно завершить такую строку как `Failed/ModelUnavailable`, вместо того чтобы оставлять её без изменений.
6. Выполнить проверки и записать в «Отчёт» результаты новых тестов.

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
