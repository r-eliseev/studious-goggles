---
id: 130
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Зафиксировать общий in-memory adapter вместо OpenRouter-specific runner

## Описание
ADR 0005 принят, но его детали противоречат выбранному пути задач 111/102/128/129: он помещает semaphore и queue position внутрь OpenRouterJobRunner, а новый единый path требует semaphore в GenerationService и общий adapter для OpenRouter+OpenAI-compatible. До кода нужен superseding ADR, не переписывание истории.

Зависит от: 121.
Среда: Linux или Windows.

## Критерии готовности
- Новый ADR 0019 принят владельцем вместе с одобрением этой задачи; ADR0005 status `заменено на 0019` и ссылка, остальной historical text не переписан.
- Один `InMemoryQueuedImageProvider` оборачивает все synchronous image transports (OpenRouter/OpenAI-compatible).
- Единственный per-provider semaphore принадлежит GenerationService и охватывает ожидание slot→submit→poll→terminal/local cancellation; adapter второго не имеет.
- До submit DB generation может быть Queued, но adapter handle/queue position ещё нет; queue position не обещается.
- Local terminal snapshot хранится 10 минут и sweep при adapter operation; unknown handle после process restart → Interrupted.
- ProviderRouter composite и единый GenerationService queued path отнесены к 128/129.

## Анализ
Это уточняет механизм, сохраняя цель ADR0005: UI не блокируется, submit возвращает local handle, общий poll/cancel, restart Interrupted. Причина замены — обнаруженный double-semaphore conflict и необходимость поддержать OpenAI-compatible synchronous transport, а не только OpenRouter.

## План
Для Haiku/Luna: docs only. Не менять architecture/code; они обновляются 129.

1. Создать `docs/decisions/0019-common-in-memory-queued-adapter.md` по template: Context; Decision из критериев; Alternatives (старый OpenRouter-specific runner/double semaphore, сохранение normal path); Consequences.
2. В ADR0005 изменить только Status на `заменено на 0019` и добавить ссылку сразу под metadata; основной текст оставить историческим.
3. В новом ADR явно назвать ownership cancellation: GenerationService local job CTS; adapter own transport CTS; Stop/Cancel связывает их через handle 121.
4. Зафиксировать TTL=10 minutes и отсутствие background timer/queue-position contract.
5. `docs/index.md` менять не нужно: decisions/ уже индексирован. check.sh.

### Проверка
```
bash scripts/check.sh
```

Файлы: `docs/decisions/0005-openrouter-background-job.md`, `docs/decisions/0019-common-in-memory-queued-adapter.md` (новый)

## Отчёт

## Комментарии
