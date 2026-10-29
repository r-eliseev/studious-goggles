---
id: 111
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Ограничить активные генерации отдельно для каждого провайдера

## Описание
Найдено ревью 2026-10-29. ADR 0003 требует семафор на провайдера. Сейчас `GenerationService` использует один глобальный `SemaphoreSlim(4)`. Для queued provider слот удерживается только на submit и освобождается до polling, поэтому удалённо может одновременно выполняться больше четырёх fal-заданий; медленные submit одного провайдера при этом блокируют другой.

Зависит от: 108.
Среда: Linux или Windows.

## Критерии готовности
- У каждого provider id свой лимит `MaxParallelGenerations = 4`.
- Пятый job одного провайдера не submit-ится/не генерируется, пока один из первых четырёх не достиг terminal state.
- Четыре активных задания provider A не мешают начать задания provider B.
- Для queued provider слот удерживается от начала submit до terminal update или отмены.
- Сбой или отмена всегда освобождает слот ровно один раз.
- Тесты не используют `Task.Delay` для синхронизации; только `TaskCompletionSource` и bounded ожидание тестового фреймворка.

## Анализ
- Текущий `_slots` глобальный и создаётся в поле `GenerationService`.
- `ExecuteQueuedAsync` освобождает `_slots` перед `WatchQueuedAsync` по старому комментарию «queue can take minutes»; это прямо противоречит ограничению активных provider jobs.
- После задачи 108 token и running job уже должны передаваться через весь pipeline; использовать их, не создавать второй cancellation-механизм.
- Пересекается с 106/108/113 по `GenerationService`; выполнять после 108 и до 113.

## План
Для исполнителя (Haiku/Luna): не переписывай provider interfaces. Сначала зафиксируй семантику тестами. Для тестовой синхронизации используй gates и счётчики `Interlocked`; никаких ожиданий «100 ms и проверить».

Прочитай: ADR 0003, `GenerationService.cs`, `GenerationServiceTests.cs`, тесты queued provider.

1. Добавить test double provider с gate и счётчиками current/max concurrency.
2. Падающий тест normal provider: пять generation одного provider → до открытия gate стартуют ровно четыре; max равно четырём; после открытия заканчиваются все.
3. Падающий тест два provider: четыре заблокированных у A не мешают одному job B войти в provider call.
4. Падающий queued-тест: первые четыре submit завершены и polling заблокирован; пятый submit ещё не вызван. После terminal update пятый запускается.
5. Заменить `_slots` на потокобезопасное хранилище `providerId → SemaphoreSlim`; создавать семафор лениво с `StringComparer.Ordinal` семантикой provider id.
6. Получать слот перед provider work. Для normal path освобождать после результата; для queued path — только после окончания watch. `finally` обязан покрывать submit, watch, exception и cancellation.
7. Удалить/обновить комментарий, который говорит, что queued slot освобождается после submit. Публичную константу лимита не менять.
8. Проверить, что recovery saved handle также получает provider slot и не обходит лимит.

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
