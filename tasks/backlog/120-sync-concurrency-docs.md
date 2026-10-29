---
id: 120
status: cancelled
assignee:
claimed:
created: 2026-10-29
---

# Уточнить документацию concurrency generation

## Описание
Architecture «не больше 4» не задаёт scope. После 111 это четыре локально отслеживаемых jobs на provider, включая fresh submit→terminal и resumed saved-handle polling; local cancellation освобождает slot, remote cancel failure остаётся limitation.

Зависит от: —
Среда: Linux или Windows.

## Критерии готовности
- Architecture точно описывает per-provider local limit 4 и независимость providers.
- Fresh queued и recovered handle занимают slot полного tracked lifecycle.
- Local cancellation/failed remote cancel limitation названа без обещания строгого remote limit.
- ADR 0003 не переписывается; internal SemaphoreSlim не документируется.

## Анализ
Concurrency docs должны меняться одновременно с лимитом 111; 129 сохраняет и уточняет этот contract при объединении путей. Требования не отменены: они переданы в 111 (per-provider local slots, recovered jobs, cancellation limitation) и 129 (итоговый единый queued путь). По `docs/tasks.md`, раздел «Завершение», документацию обязан обновлять исполнитель изменения поведения. Отдельная задача дублировала эту обязанность и создавала ложную зависимость README/других задач. Исторические критерии выше сохранены для трассировки.

## План
Снята в ходе ревью по поручению владельца 2026-10-29. Не занимать и не выполнять отдельно. При выполнении задач 111 (per-provider local slots, recovered jobs, cancellation limitation) и 129 (итоговый единый queued путь) сверить соответствующие критерии выше с code/tests и обновить docs в той же ветке.

## Отчёт
2026-10-29: отменена как дублирующая задача; код не менялся. Concurrency docs должны меняться одновременно с лимитом 111; 129 сохраняет и уточняет этот contract при объединении путей. Ответственность перенесена в 111 (per-provider local slots, recovered jobs, cancellation limitation) и 129 (итоговый единый queued путь). Это не утверждение о готовности будущего поведения.

## Комментарии
