---
id: 119
status: cancelled
assignee:
claimed:
created: 2026-10-29
---

# Уточнить документацию восстановления generation

## Описание
S3.8 и walking-skeleton summary говорят общей «Interrupted при старте». Code различает saved handle resume, no handle Interrupted и после 106 missing provider ModelUnavailable.

Зависит от: —
Среда: Linux или Windows.

## Критерии готовности
- S3.8 и skeleton summary: saved valid handle → resume polling/no submit; no handle → `GenerationStatus.Failed` + `ErrorKind.Interrupted`; unavailable provider → Failed/ModelUnavailable.
- Architecture first-start/run paragraphs те же.
- Corrupt `job_json` → `GenerationStatus.Failed` + `ErrorKind.Unknown`, как закреплено catch/test задачи 106.
- SQL `job_json IS [NOT] NULL` semantics не искажены.

## Анализ
Recovery docs должны совпадать с поведением 106, а не ждать отдельного документационного PR после 129. Требования не отменены: они переданы в 106 (recovery, S3.8, «Первый запуск») и 129 (единый queued lifecycle и финальное описание «Прогона»). По `docs/tasks.md`, раздел «Завершение», документацию обязан обновлять исполнитель изменения поведения. Отдельная задача дублировала эту обязанность и создавала ложную зависимость README/других задач. Исторические критерии выше сохранены для трассировки.

## План
Снята в ходе ревью по поручению владельца 2026-10-29. Не занимать и не выполнять отдельно. При выполнении задач 106 (recovery, S3.8, «Первый запуск») и 129 (единый queued lifecycle и финальное описание «Прогона») сверить соответствующие критерии выше с code/tests и обновить docs в той же ветке.

## Отчёт
2026-10-29: отменена как дублирующая задача; код не менялся. Recovery docs должны совпадать с поведением 106, а не ждать отдельного документационного PR после 129. Ответственность перенесена в 106 (recovery, S3.8, «Первый запуск») и 129 (единый queued lifecycle и финальное описание «Прогона»). Это не утверждение о готовности будущего поведения.

## Комментарии
