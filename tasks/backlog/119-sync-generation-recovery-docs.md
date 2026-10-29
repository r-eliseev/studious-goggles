---
id: 119
status: approved
assignee:
claimed:
created: 2026-10-29
---

# Уточнить документацию восстановления generation

## Описание
S3.8 и walking-skeleton summary говорят общей «Interrupted при старте». Code различает saved handle resume, no handle Interrupted и после 106 missing provider ModelUnavailable.

Зависит от: 106, 117, 129.
Среда: Linux или Windows.

## Критерии готовности
- S3.8 и skeleton summary: saved valid handle → resume polling/no submit; no handle → `GenerationStatus.Failed` + `ErrorKind.Interrupted`; unavailable provider → Failed/ModelUnavailable.
- Architecture first-start/run paragraphs те же.
- Corrupt `job_json` → `GenerationStatus.Failed` + `ErrorKind.Unknown`, как закреплено catch/test задачи 106.
- SQL `job_json IS [NOT] NULL` semantics не искажены.

## Анализ
Общий docs conflict order 117 →119 →120. Historical docs не менять.

## План
Для Haiku/Luna: только architecture+chat spec после rebase 117.

1. Сверить RecoverInterrupted, Watch catch, ListResumable/MarkUnfinished SQL after106.
2. Переписать S3.8 и строку «Ходячий скелет» с точными domain status/kind.
3. Обновить architecture «Первый запуск» и «Прогон», без retry internals.
4. Grep Interrupted/прерван; active docs consistent, historical untouched.

### Проверка
```
grep -rn "Interrupted\|прерван" docs/architecture.md docs/specs --include='*.md'
bash scripts/check.sh
```

Файлы: `docs/architecture.md`, `docs/specs/chat-sessions.md`

## Отчёт

## Комментарии
