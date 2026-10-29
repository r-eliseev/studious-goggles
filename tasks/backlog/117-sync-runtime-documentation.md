---
id: 117
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Синхронизировать документацию контекста ассистента

## Описание
Найдено ревью 2026-10-29. `docs/architecture.md` и S2.1/S2.6 `docs/specs/chat-sessions.md` продолжают описывать фиксированные последние 20 сообщений, хотя ADR 0015 и текущая реализация используют context budget и rolling summary. «20» теперь означает защищённую свежую часть при планировании summary, а не максимальный размер request.

Зависит от: 110.
Среда: Linux или Windows.

## Критерии готовности
- Основной chat flow описывает fixed system/current prompt, optional summary и историю по context budget.
- S2.1/S2.6 не противоречат C1–C3 `assistant-context.md` и не дублируют алгоритм целиком.
- Последние 20 упоминаются только как свежая часть summary policy и только с оговоркой «если помещаются».
- Статус `assistant-context.md` «реализовано» подтверждён C1.6 после задачи 110.

## Анализ
- `assistant-context.md` прямо говорит, что заменяет старое S2.6.
- `AssistantService.BuildRequestAsync` использует `ContextBudget` и `ContextBuilder`; `ConversationSummaryService` поддерживает rolling summary.
- Recovery generation и provider concurrency вынесены в задачи 119 и 120, чтобы небольшая модель не смешивала независимые темы.

## План
Для исполнителя (Haiku/Luna): задача только документационная. Перед правкой сверь текст с кодом после 110. Не добавляй номера задач и ход работ в architecture/spec.

Прочитай: `docs/architecture.md` paragraph «Сообщение в чат», S2 `docs/specs/chat-sessions.md`, `docs/specs/assistant-context.md`, ADR 0015, `AssistantService.BuildRequestAsync`, `ConversationSummaryService`.

1. В `docs/architecture.md` заменить «последние 20 сообщений» на краткое описание budget+summary и ссылку на `assistant-context.md`.
2. В `chat-sessions.md` переписать S2.1 как текущее contract-поведение. S2.6 сократить до ссылки на C1–C3 и убрать слово «сейчас» про старый fixed window.
3. Проверить C1.6 и статус `assistant-context.md` после 110; если реализация расходится, поставить `blocked`, не объявлять её реализованной.
4. Grep всех упоминаний «последние 20»: исторические logs/ADR не менять; в active docs каждое совпадение должно иметь корректный смысл.

### Проверка
```
grep -rn "последние 20" docs/architecture.md docs/specs --include='*.md'
bash scripts/check.sh
```

Файлы: `docs/architecture.md`, `docs/specs/chat-sessions.md`, `docs/specs/assistant-context.md` (только статус/формулировка C1.6 при необходимости)

## Отчёт

## Комментарии
