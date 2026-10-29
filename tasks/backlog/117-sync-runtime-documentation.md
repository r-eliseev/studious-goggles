---
id: 117
status: approved
assignee:
claimed:
created: 2026-10-29
---

# Синхронизировать документацию контекста ассистента

## Описание
Architecture и S2.1/S2.6 всё ещё описывают fixed last 20, хотя ADR 0015/code используют context budget+summary. «20» — защищённая свежая часть summary policy, не request maximum.

Зависит от: 110, 123.
Среда: Linux или Windows.

## Критерии готовности
- Chat flow: fixed system/current, optional summary, history по context budget.
- S2.1/S2.6 ссылаются на C1–C3 без дублирования.
- Last 20 только в корректном «если помещаются»/summary смысле.
- `assistant-context.md` остаётся «реализовано» только если после 110/123 все C1–C3 подтверждены code/tests; иначе задача blocked, contract не подгоняется.

## Анализ
117,119,120 меняют общие docs. Обязательный порядок против конфликтов: 117 → 119 → 120.

## План
Для Haiku/Luna: docs only; сверять после dependencies, не писать task progress в architecture.

1. Architecture chat paragraph заменить кратким budget+summary и ссылкой.
2. S2.1 сделать current contract; S2.6 только ссылка C1–C3, убрать «сейчас fixed 20».
3. Проверить C1–C3 и status после 110/123; любое расхождение → blocked с code/test lines.
4. Grep last 20; historical ADR/log не менять.

### Проверка
```
grep -rn "последние 20" docs/architecture.md docs/specs --include='*.md'
bash scripts/check.sh
```

Файлы: `docs/architecture.md`, `docs/specs/chat-sessions.md`, `docs/specs/assistant-context.md` (status only if evidence requires)

## Отчёт

## Комментарии
