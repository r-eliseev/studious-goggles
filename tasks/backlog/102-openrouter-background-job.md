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

Сначала нужно решение владельца (`docs/status.md`, «Открытые вопросы»): выполнить ADR 0005 или заменить его новым ADR, оставив OpenRouter синхронным. Если второе — задача сводится к новому ADR, статусу «заменено на NNNN» у 0005 и правке `docs/architecture.md`.

Зависит от: решения владельца.
Среда: Linux или Windows.

## Критерии готовности
- Решение владельца записано в «Комментариях».
- Если ADR выполняется: OpenRouter-генерации идут через `IQueuedImageProvider` (или общий контракт ADR 0003) с заданием в памяти процесса, `GenerationService` имеет один путь, `IImageGenerator` удалён или оставлен с обоснованием; тесты ядра на submit, poll, отмену и перезапуск (задание в памяти теряется → «Interrupted»).
- Если ADR заменяется: новый ADR, 0005 помечен «заменено на NNNN», `docs/architecture.md` описывает два пути как задуманные.
- `docs/architecture.md` совпадает с кодом.

## Анализ

## План

## Отчёт

## Комментарии
