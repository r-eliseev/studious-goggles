---
id: 116
status: approved
assignee:
claimed:
created: 2026-10-29
---

# Заменить Replicate в нормативной документации продукта

## Описание
ADR 0017 выбирает Together AI вместо Replicate через manual `providers.json` как `openAiCompatible`. Built-in config содержит только OpenRouter/fal; Together live response/price не проверены. Нормативные active sections всё ещё обещают Replicate. Исторические ADR/log/tasks/research факты не переписываются.

Зависит от: —
Среда: Linux или Windows.

## Критерии готовности
- AGENTS/product различают built-in OpenRouter/fal и выбранный, но manually configured Together AI.
- Нормативные product requirements/terms/API overview не требуют Replicate client.
- Together не назван built-in/проверенным; unknown live response/price сохранены.
- Датированные исследования сторонних приложений, ADR context, logs, tasks, poc не изменены.
- Финальный список Replicate occurrences классифицирован в отчёте как допустимый historical/research; запрещённых normative occurrences нет.

## Анализ
Большой `image-client.md` смешивает contract и датированное исследование. Нельзя механически заменить весь grep. Править только: «Цель и охват», текущий product provider/API overview и выводы, F1 provider setup, F2.5 configurable model/provider, F7.2 provider filter, active terms/mockups если они называют current provider. Historical competitor comparison остаётся с датой.

`docs/knowledge/csharp.md` active HTTP/JSON guidance содержит Replicate-specific auth/schema и тоже обновляется общими подтверждёнными формулировками.

## План
Для Haiku/Luna: docs only, no internet/new facts. Перед edit составить таблицу occurrence → normative или historical; uncertain occurrence = `blocked`, не угадывать.

Прочитай: ADR 0017/0009, builtin-providers.json, ProviderKind/Loader, AGENTS, product, headings image-client, csharp knowledge, status Together open question.

1. Grep всех `Replicate`; в отчёт предварительную классификацию. Не трогать decisions/log/tasks/poc.
2. AGENTS: built-ins OpenRouter/fal; additional OpenAI-compatible config; Together selected but not built-in/live-verified.
3. product: purpose/provider schema/extra requirements/terms. Не обещать same UI key flow для manually configured Together.
4. image-client только нормативные sections из Analysis. Replicate-specific expiry/prediction/safety examples убрать/обобщить; dated research оставить и при необходимости явно назвать исследованием 2026-10-27.
5. csharp knowledge: auth/schema guidance общая или по реально существующим clients; historical facts не добавлять.
6. Final grep и report allowed occurrences with reason.

### Проверка
```
grep -rn "Replicate" AGENTS.md docs --include='*.md'
bash scripts/check.sh
```

Файлы: `AGENTS.md`, `docs/product.md`, `docs/specs/image-client.md`, `docs/knowledge/csharp.md`

## Отчёт

## Комментарии
