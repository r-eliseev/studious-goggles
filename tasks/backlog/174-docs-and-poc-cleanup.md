---
id: 174
status: done
assignee:
claimed:
created: 2026-10-31
---

# Убрать устаревшие документы и пробный код

## Описание
Решение владельца 2026-10-31: документации и прочих файлов (кроме кода и задач) слишком много — 190 файлов, из них ~55 пробного кода к закрытым задачам и 1.1 МБ материалов выбора дизайна. Удалённое остаётся в истории git. ADR не удаляются и не переписываются (`AGENTS.md`, «Запреты»).

Зависит от: —
Среда: Linux или Windows. Разрешены правки `AGENTS.md`, `docs/tasks.md` и удаление `docs/audit.md`, `docs/harness.md`, `docs/subagents.md` (п. 3 и 8 плана); в `scripts/` — только две строки `scripts/audit.sh` (п. 8); остальные скрипты и `.github/` не менять.

## Критерии готовности
- Удалены: `poc/v0.1/*`, `poc/backlog/040-*`, `poc/backlog/081-*`; `docs/design/screenshots/`, `docs/design/directions.html`, `docs/design/prototype.html`; `docs/knowledge/kiro-web-session.md`; журналы `docs/log/*.md` (кроме шаблона) — их выводы сведены в один файл.
- Объединены: описания сред — в `docs/knowledge/dev-environment.md`; `avalonia-maui-backend.md` — в `avalonia.md`; `docs/design/research.md` — в `implementation.md`; из `docs/specs/image-client.md` убрано датированное исследование провайдеров и чужих приложений; постоянные решения владельца из `docs/status.md` перенесены в `docs/product.md` (или существующие ADR), в `status.md` остаются открытые вопросы, очередь и известные проблемы.
- Нет битых ссылок: в `docs/`, `AGENTS.md`, `README.md`, задачах (кроме архивных строк `tasks/DONE.md`), комментариях кода и тестах; `docs/index.md` соответствует файлам; `tokens.json` и всё, на что ссылаются тесты, на месте.
- Дополнительно (решение владельца 2026-10-31): `docs/specs/assistant-context.md` влит в `chat-sessions.md` (S2.6); `docs/knowledge/avalonia-mobile.md` — в `avalonia.md`; `docs/knowledge/together-ai.md` — в новый `docs/knowledge/providers.md`; `docs/design/README.md` — в `implementation.md`.
- Правила работы: `docs/harness.md` перенесён в `AGENTS.md`; `docs/audit.md` и `docs/subagents.md` влиты разделами в `docs/tasks.md` (сам `docs/tasks.md` остаётся — его требует `scripts/check-docs.sh`); `scripts/audit.sh` указывает на раздел «Аудит» в `docs/tasks.md`.
- `scripts/check.sh` и `scripts/audit.sh` проходят; тесты (в том числе `Presentation.Tests/Design`) зелёные.

## Анализ
Инвентаризация на `e463462` (без `ImageGen/` и `tasks/`): `poc/` 79 файлов (660 КБ), `docs/design/screenshots` 1.1 МБ, `docs/log` 19 журналов, `docs/knowledge` 11 файлов, `docs/decisions` 24 ADR.
- `poc/README.md`: пробный код «не поддерживается в актуальном состоянии»; задачи v0.1 и 040/081 закрыты, их код в `ImageGen/`, выводы 081 — в ADR 0016. `poc/backlog/094` остаётся: Avalonia для Android отложена, ADR 0019 «предложено» (решение владельца 2026-10-31).
- `scripts/check-docs.sh` требует, чтобы каждый файл `docs/` был упомянут в `index.md` (кроме `log/` и `_*.md`) — при удалении/слиянии править `index.md`. Скрипты на удаляемые файлы не ссылаются; тесты используют `docs/design/tokens.json` (поиск корня) — не трогать.
- Правило «работа вне задачи → `docs/log/…`» есть в `AGENTS.md` («Когда что обновлять») и в `docs/audit.md` («Итог записывается в `docs/log/YYYY-MM-DD-audit.md`»). Без его изменения журналы будут снова копиться.

## План
1. **Ссылки.** До удаления собрать все упоминания удаляемых и сливаемых путей (`grep -rn` по репозиторию, кроме `.git`) — список в «Отчёт». Ссылки из живых документов, задач, кода и тестов переписать на новое место; исторические строки `tasks/DONE.md` не править.
2. **Удалить** (`git rm`): `poc/v0.1/`, `poc/backlog/040-avalonia-secret-store/`, `poc/backlog/081-semantic-search-research/` (ссылку в плане 105 заменить на ADR 0016); `docs/design/screenshots/`, `directions.html`, `prototype.html` (ссылки в `docs/design/README.md`, `implementation.md`, ADR 0014 — ADR не переписывать: в нём оставить как есть, история в git); `docs/knowledge/kiro-web-session.md`. В `poc/README.md` убрать раздел v0.1.
3. **Журналы.** Новый `docs/history.md` (в `index.md`): по одной-две строки на каждый из 19 журналов — дата, что сделано, где решение (ADR, задача, `status.md`); затем удалить журналы, оставить `docs/log/_template.md`. Правило: `AGENTS.md` — журнал только для аудита (`docs/log/YYYY-MM-DD-audit.md`), прочая работа вне задач — строка в `docs/history.md`; согласовать `docs/audit.md`, `docs/harness.md`, `docs/tasks.md`.
4. **Слить:** `claude-cloud-session.md`, `vps-session.md`, `windows-machine.md` → разделы `docs/knowledge/dev-environment.md` (факты дословно, без потери «проверено/не проверено» и дат); `avalonia-maui-backend.md` → раздел `avalonia.md`; `docs/design/research.md` → раздел `implementation.md`.
5. **`image-client.md`:** убрать разделы «Как работают API провайдеров» (датированное исследование, включая Replicate), «Функции найденных приложений», «Источники»; требования F*, экраны, сущности и ограничения оставить. Если факт из исследования нужен (например, сроки хранения файлов fal) — перенести строкой в `docs/knowledge/` с датой.
6. **`status.md`:** постоянные решения владельца (разделы «Решения владельца …») → `docs/product.md`, раздел «Решения» с датами (или ссылка на соответствующий ADR); исторические абзацы аудитов → `docs/history.md`. В `status.md` остаются: CI, очередь/доступность задач, открытые вопросы, известные проблемы.
7. **Ещё слить** (решение владельца): `docs/specs/assistant-context.md` → раздел S2.6 `chat-sessions.md` (статус и код-ссылки сохранить, ADR 0015 ссылается — ADR не править, ссылка в нём остаётся исторической); `docs/knowledge/avalonia-mobile.md` → раздел «Android и iOS (исследование 094)» в `avalonia.md`; `docs/knowledge/together-ai.md` → `docs/knowledge/providers.md` (раздел Together; сюда же позже факты 170); `docs/design/README.md` → вводная часть `implementation.md`.
8. **Правила работы.** `docs/harness.md` → раздел `AGENTS.md` «Как устроен харнес» (сжато: структура репозитория, ветки, параллельная работа; без повторов уже имеющихся в `AGENTS.md` правил). `docs/audit.md` → раздел «Аудит» в `docs/tasks.md`; `docs/subagents.md` → раздел «Вспомогательные агенты» в `docs/tasks.md`; ссылки в `AGENTS.md` («Вспомогательные агенты», «Аудит») переписать на эти разделы. В `scripts/audit.sh` (разрешено владельцем) заменить `docs/audit.md` на `docs/tasks.md`, раздел «Аудит» — в комментарии строки 6 и в выводе строки 65; остальные скрипты не менять (их ссылки на `docs/tasks.md` остаются верными).
9. Проверки: `scripts/check.sh`, `scripts/audit.sh`, Presentation.Tests (тесты дизайна), поиск битых путей (`grep` по списку из п. 1 — пусто), `git diff --stat` в «Отчёт».

Файлы: удаляемые — п. 2–3; изменяемые — `AGENTS.md`, `scripts/audit.sh` (две строки), `docs/specs/chat-sessions.md`, `docs/knowledge/providers.md` (новый), удаляемые также `docs/specs/assistant-context.md`, `docs/knowledge/avalonia-mobile.md`, `docs/knowledge/together-ai.md`, `docs/design/README.md`, `docs/harness.md`, `docs/audit.md`, `docs/subagents.md`; `README.md` (если ссылается), `poc/README.md`, `docs/index.md`, `docs/history.md` (новый), `docs/audit.md`, `docs/harness.md`, `docs/tasks.md`, `docs/status.md`, `docs/product.md`, `docs/specs/image-client.md`, `docs/knowledge/dev-environment.md`, `docs/knowledge/avalonia.md`, `docs/design/README.md`, `docs/design/implementation.md`, `tasks/backlog/105-semantic-text-search.md` и другие файлы со ссылками из п. 1.

## Отчёт

- Удалены старые POC v0.1, 040 и 081, макеты/снимки дизайна, Kiro Web, отдельные документы аудита/харнеса и спецификация assistant-context; POC 094 сохранён. Удалено 18 журналов, которые реально были в `docs/log/` (в карточке было указано 19); краткие датированные записи собраны в `docs/history.md`, шаблон журнала оставлен.
- Объединены документы сред в `docs/knowledge/dev-environment.md`, сведения о мобильном Avalonia и Avalonia MAUI Backend в `docs/knowledge/avalonia.md`, Together AI в новый `docs/knowledge/providers.md`, рекомендации и исследование дизайн-системы в `docs/design/implementation.md`, а правила аудита/делегирования — в `docs/tasks.md`. Харнес перенесён в `AGENTS.md`; постоянные решения собраны в `docs/product.md`, `status.md` оставлен для CI, очереди, открытых вопросов и проблем. Из `image-client.md` убраны устаревшее исследование API/приложений и источники.
- Активные ссылки перенесены на актуальные документы; локальные Markdown-ссылки проверены автоматически — битых ссылок нет. Исторические ссылки внутри ADR не изменялись по прямому указанию в плане; ссылки на удалённые материалы там остаются историческими. `docs/design/tokens.json` не тронут.
- Изменён `scripts/audit.sh` только в двух строках, как прямо разрешено задачей; остальных скриптов и workflow это изменение не касается.
- Проверено: `dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj` — 420/420; `scripts/check.sh`; `scripts/audit.sh` (показывает только предложенный ADR 0019, решение по мобильной оболочке остаётся за владельцем); `git diff --check`; локальная проверка Markdown-ссылок — 0 ошибок.
- Итоговый diff: 119 файлов, 434 добавления и 5518 удалений на момент измерения; основной объём — удалённые устаревшие POC и макетные материалы. Изменений кода продукта нет, кроме обновления комментария со ссылкой на ADR.

## Комментарии

### 2026-10-31, владелец (в чате, записал claude-audit)
Включить всё: старый poc, материалы дизайна, журналы docs/log, объединение документов. Kiro Web не используется — удалить. Avalonia для Android отложена, ADR 0019 остаётся «предложено», `poc/backlog/094` не трогать. Объём одобрен (выбран в вопросе).

### 2026-10-31, владелец (в чате, записал claude-audit)
Добавить: слияние спецификаций и знаний (assistant-context, avalonia-mobile, together-ai, design/README) и перенос правил работы в AGENTS.md. `scripts/` не трогать — `docs/tasks.md` остаётся, в него вливаются audit и subagents. ADR не сводить.

### 2026-10-31, владелец (в чате, записал claude-audit)
Разрешено исправить `scripts/audit.sh`: ссылку на `docs/audit.md` заменить на раздел «Аудит» в `docs/tasks.md` — в рамках этой задачи, вместе с переносом файла.
