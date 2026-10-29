---
id: 117
status: in-progress
assignee: opencode-vps
claimed: 2026-10-29T17:27:14Z
created: 2026-10-29
---

# Синхронизировать документацию контекста ассистента

## Описание
Architecture и S2.1/S2.6 всё ещё описывают fixed last 20, хотя ADR 0015/code используют context budget+summary. «20» — защищённая свежая часть summary policy, не request maximum.

Зависит от: —.
Среда: Linux или Windows.

## Критерии готовности
- Chat flow: fixed system/current, optional summary, history по context budget.
- S2.1/S2.6 ссылаются на C1–C3 без дублирования.
- Last 20 только в корректном «если помещаются»/summary смысле.
- Нормативные C1–C4 не подгоняются под пробелы реализации. Если C1.6 ещё отсутствует, статус assistant-context честно «в работе» с кратким указанием несоответствия; задача 117 не блокируется ожиданием 110 или 123 и не пишет их код. Если 110 уже merged, статус определяется сверкой code/tests.

## Анализ
Устаревший fixed20 расходится с уже существующими `AssistantService.BuildRequestAsync`, `ContextBuilder`, `SummaryPlanner` и ADR0015; исправление нужно независимо от 110/123.119/120 сняты: recovery/concurrency документацию обновляют 106/111/129 в рамках собственных изменений. Общие docs править узко; при rebase сохранить независимые абзацы recovery/concurrency.

Оговорка: C1.6 сейчас отсутствует, это предмет 110; 123 меняет accepted-turn failure, а не правило last20.117 не объявляет все C1–C4 реализованными по наличию одного нового абзаца и не переписывает accepted-turn flow.

## План
Для Haiku/Luna: docs only; сверять текущую main на момент реализации. В architecture описывать contract, не номера задач, статусы или порядок работ.

1. Прочитать `AssistantService.BuildRequestAsync`, `ContextBuilder.Select`, `SummaryPlanner.Plan`, `ConversationSummaryService` и context tests. В отчёте сопоставить: request выбирает сообщения по бюджету; current user message сохраняется; summary защищает последние20 от свёртки; last20 не является maximum запроса.
2. В architecture chat paragraph заменить fixed20 на порядок system/current-prompt → optional summary → history по бюджету → generate_images tool, со ссылкой на assistant-context. В S2.1 написать тот же краткий contract; S2.6 сделать ссылкой на C1–C4 и ADR0015, убрать «сейчас fixed20». Не копировать все constants/алгоритмы в три документа.
3. Сверить status assistant-context с кодом и тестами: при отсутствующем C1.6 поставить «в работе», сохранить исходное требование и описать известный gap короткой строкой у статуса. Не менять C1.6 на то, что делает текущий код, и не объявлять новую unknown, если gap уже описан 110. Если 110 уже merged и критерии spec подтверждены, оставить реализовано с evidence в Отчёте.
4. `rg -n "последние 20|HistoryMessageCount|C1\.6" docs/architecture.md docs/specs ImageGen/ImageGen.Core/Context ImageGen/ImageGen.Core/Services`: все оставшиеся20 относятся к summary protection/условию «если помещаются», не к фиксированному request cap. Historical ADR/log не менять.
5. После проверки заполнить «Отчёт» со ссылками на сверенные code/docs и оставшимися неизвестными; выполнить `scripts/release-task.sh 117` и `bash scripts/check.sh`, обновить PR по `docs/tasks.md`. Код приложения не менять.

### Проверка
```
rg -n "последние 20" docs/architecture.md docs/specs -g '*.md'
bash scripts/check.sh
```

Файлы: `docs/architecture.md`, `docs/specs/chat-sessions.md`, `docs/specs/assistant-context.md` (status only if evidence requires)

## Отчёт

## Комментарии
