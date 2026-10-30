---
id: 130
status: in-progress
assignee: codex-luna
claimed: 2026-10-30T09:16:57Z
created: 2026-10-29
---

# Зафиксировать общий in-memory adapter вместо OpenRouter-specific runner

## Описание
ADR 0005 принят, но его детали противоречат выбранному пути задач 111/102/128/129: он помещает semaphore и queue position внутрь OpenRouterJobRunner, а новый единый path требует semaphore в GenerationService и общий adapter для OpenRouter+OpenAI-compatible. До кода нужен superseding ADR, не переписывание истории.

Зависит от: —
Среда: Linux или Windows.

## Критерии готовности
- Новый ADR со следующим свободным номером в `docs/decisions/` принят владельцем вместе с одобрением этой задачи; на момент ревью свободен0019. ADR0005 status `заменено на NNNN` и ссылка, остальной historical text не переписан. При занятом0019 использовать следующий номер и обновить ссылки в изменяемых документах/планах, не перезаписывать чужой ADR.
- Один `InMemoryQueuedImageProvider` оборачивает все synchronous image transports (OpenRouter/OpenAI-compatible).
- Единственный per-provider semaphore принадлежит GenerationService: job сначала ожидает slot, затем удерживает его submit→poll→terminal/local cancellation; adapter второго не имеет.
- До submit DB generation может быть Queued, но adapter handle/queue position ещё нет; queue position не обещается.
- Local terminal snapshot хранится 10 минут и sweep при adapter operation; unknown handle после process restart → Interrupted.
- ProviderRouter composite и единый GenerationService queued path отнесены к128/129.128 не включает queued capability в production-dispatch;129 включает её атомарно вместе с orchestration.
- Polling intervals: native fal минимум1s/максимум5s, in-memory adapter минимум50ms/максимум250ms; первый Poll без начальной задержки, network backoff/retry fal сохраняются. Источник выбора локального режима — connection wrapper InMemoryQueuedImageConnection, не provider id (custom compatible ids также local).

## Анализ
Это уточняет механизм, сохраняя цель ADR0005: UI не блокируется, submit возвращает local handle, общий poll/cancel, restart Interrupted. Причина замены — обнаруженный double-semaphore conflict и необходимость поддержать OpenAI-compatible synchronous transport, а не только OpenRouter.

Нужность: убирает противоречие принятого ADR0005 с общей поддержкой compatible transports и не допускает два semaphore. 121 не предпосылка для ADR: здесь описывается контракт, без чтения будущей реализации. 130 открывает независимую цепочку102 →128, пока lifecycle задачи идут отдельно.

## План
Для Haiku/Luna: docs only. Не менять architecture/code; их обновляют кодовые задачи111/129. Имя будущего ADR в планах102/129 означает ADR этой задачи; использовать фактический присвоенный номер.

1. Проверить свободный номер перед созданием; создать `docs/decisions/NNNN-common-in-memory-queued-adapter.md` по template: Context; Decision из критериев; Alternatives (старый OpenRouter-specific runner/double semaphore, сохранение normal path); Consequences.
2. В ADR0005 изменить только Status на `заменено на NNNN` и добавить ссылку сразу под metadata; основной текст оставить историческим.
3. В новом ADR явно назвать ownership cancellation: GenerationService local job CTS; adapter own transport CTS; Stop/Cancel связывает их через handle 121.
4. Зафиксировать TTL=10minutes (sweep удаляет terminal при elapsed>=TTL), отсутствие background timer/queue-position contract и точные polling intervals из критериев. Локальный handle использует память текущего процесса: после restart Poll возвращает Interrupted, не повторяет оплаченный запрос.
5. `docs/index.md` менять не нужно: decisions/ уже индексирован. check.sh.

### Проверка
```
bash scripts/check.sh
```

Файлы: `docs/decisions/0005-openrouter-background-job.md`, `docs/decisions/NNNN-common-in-memory-queued-adapter.md` (новый, следующий свободный номер)

После проверок заполнить «Отчёт» (изменения, проверки, непроверенное), выполнить `scripts/release-task.sh 130`, затем `bash scripts/check.sh`; отправить PR по `docs/tasks.md`.

## Отчёт

## Комментарии
