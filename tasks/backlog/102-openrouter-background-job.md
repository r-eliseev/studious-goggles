---
id: 102
status: todo
assignee:
claimed:
created: 2026-10-29
---

# OpenRouter как задание в фоне (ADR 0005)

## Описание
Найдено аудитом 2026-10-29 (`docs/log/2026-10-29-audit.md`). ADR 0005 (`docs/decisions/0005-openrouter-background-job.md`, «принято») требует, чтобы OpenRouter работал за контрактом submit/poll/cancel (ADR 0003) как задание в фоне в памяти процесса (`OpenRouterJobRunner`), а узкие `IImageGenerator` и `IModelProvider` ушли. Задача 025 закрыта с «отличий от плана нет», но в коде этого нет:
- `ImageGen/ImageGen.Core/OpenRouter/` содержит только `OpenRouterClient.cs`; `OpenRouterJobRunner`, `OpenRouterImageProvider`, `IImageProvider` не существуют;
- `ImageGen/ImageGen.Core/Providers/IImageGenerator.cs` и `IModelProvider.cs` на месте;
- `GenerationService` ведёт два пути: синхронный `GenerateAndStoreAsync` через `IImageGenerator` (семафор на 4) и очередь `ExecuteQueuedAsync`/`WatchQueuedAsync` для `IQueuedImageProvider` (fal).

Последствия: отмена (задача 024) для OpenRouter невозможна единым путём; у генерации OpenRouter после перезапуска нет хэндла, она помечается «Interrupted».

Решение владельца уже записано в `docs/status.md`: ADR 0005 выполнять. Сначала завершить узкие lifecycle-задачи 106–113, затем детализировать эту задачу под актуальный `GenerationService`; не предлагать вариант отмены ADR заново.

Зависит от: 113.
Среда: Linux или Windows.

## Критерии готовности
- Решение владельца выполнить ADR 0005 сохранено в `docs/status.md`.
- OpenRouter-генерации идут через `IQueuedImageProvider` (или общий контракт ADR 0003) с заданием в памяти процесса, `GenerationService` имеет один путь, `IImageGenerator` удалён или оставлен с обоснованием; тесты ядра на submit, poll, отмену и перезапуск (задание в памяти теряется → «Interrupted»).
- `docs/architecture.md` совпадает с кодом.

## Анализ

## План

## Отчёт

## Комментарии
