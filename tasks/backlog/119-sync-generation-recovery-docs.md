---
id: 119
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Уточнить документацию восстановления генераций

## Описание
Найдено ревью 2026-10-29. S3.8 `docs/specs/chat-sessions.md` говорит, что все незавершённые генерации после старта становятся `Interrupted`. Реализация различает queued jobs с сохранённым handle, которые возобновляют polling без повторного submit, и задания без handle. Задача 106 также задаёт terminal-поведение для handle удалённого provider.

Зависит от: 106.
Среда: Linux или Windows.

## Критерии готовности
- S3.8 различает resumable queued handle и no-handle generation.
- Architecture описывает: saved handle → resume polling без submit; no handle → `Failed/Interrupted`; unavailable provider → результат из 106.
- Документ не обещает восстановление синхронного provider job без handle.
- Формулировки совпадают с SQL selection `job_json IS [NOT] NULL`.

## Анализ
- `RunRepository.ListResumableJobsAsync` выбирает Queued/Running с `job_json IS NOT NULL`.
- `MarkUnfinishedAsInterruptedAsync` меняет только строки с `job_json IS NULL`.
- До 106 provider, удалённый из конфигурации, оставался незавершённым; документировать новое terminal-поведение можно только после 106.

## План
Для исполнителя (Haiku/Luna): только два маленьких документа. Не менять код, ADR и исторические logs. Каждое утверждение сверить с актуальными `GenerationService` и SQL.

Прочитай: `GenerationService.RecoverInterruptedAsync`, `RunRepository.ListResumableJobsAsync`, `RunRepository.MarkUnfinishedAsInterruptedAsync`, `docs/architecture.md` «Первый запуск»/«Прогон», S3.8.

1. Переписать S3.8 тремя случаями: saved valid handle; no handle; unavailable provider.
2. В `docs/architecture.md` привести «Первый запуск» и paragraph «Прогон» к той же семантике. Не дублировать детали retry polling.
3. Проверить grep `Interrupted` в active architecture/spec; исторические документы не менять.

### Проверка
```
grep -rn "Interrupted\|прерван" docs/architecture.md docs/specs --include='*.md'
bash scripts/check.sh
```

Файлы: `docs/architecture.md`, `docs/specs/chat-sessions.md`

## Отчёт

## Комментарии
