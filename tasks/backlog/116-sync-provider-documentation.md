---
id: 116
status: done
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

`docs/knowledge/csharp.md` active HTTP/JSON guidance содержит Replicate-specific auth/schema и тоже обновляется общими подтверждёнными формулировками. Задача нужна: иначе исполнители будут реализовывать уже отменённого провайдера. Зависимости нет: выбор Together принят ADR0017 и документацию о выбранном, но ещё manual provider можно исправить до реализации 132/136. После 136 именно 136 обязан обновить AGENTS/product/spec/architecture, убрав временное ограничение manual-only; 116 не выдаёт будущую поддержку за существующую.

## План
Для Haiku/Luna: docs only, no internet/new facts. Перед edit составить таблицу occurrence → normative или historical; сомнительное historical/research упоминание сохранить с причиной в отчёте/комментарии, не заменять по догадке и не блокировать всю задачу из-за формулировки, которую безопасно оставить. Если обнаружится настоящее противоречие принятого решения владельца, отдельно описать его в комментарии.

Прочитай: ADR 0017/0009, builtin-providers.json, ProviderKind/Loader, AGENTS, product, headings image-client, csharp knowledge, status Together open question.

1. `rg -n "Replicate|Together" AGENTS.md docs`: составить таблицу file/section → normative или dated evidence и план правки. Категории: текущая product requirement, выбранный provider, исходное датированное API research, competitor comparison, история решения. Decisions/log/tasks/poc и датированные факты исследования не менять.
2. AGENTS: built-ins OpenRouter/fal; additional OpenAI-compatible config; Together selected but not built-in/live-verified.
3. product: purpose/provider schema/extra requirements/terms перевести на OpenRouter/fal + выбранный Together через OpenAI-compatible config. Убрать нормативное требование Replicate-specific prediction client/schema; не обещать same UI key flow для manually configured Together. Ручные модели описывать только в пределах существующей config schema, не приписывать Together schema-discovery.
4. В image-client проверить не только F1/F2.5/F7.2, но и normative выводы после API comparison, основные сценарии, примеры UI/макеты, «Нефункциональные требования» и риски. Replicate-specific expiry/prediction/safety примеры там убрать или обобщить без неподтверждённых Together фактов. Датированную таблицу API27.10.2026 и competitor comparison сохранить как исследование исходного набора провайдеров; добавить короткую границу между историческим evidence и текущим выбором ADR0017, чтобы таблица не выглядела требованием на Replicate client.
5. csharp knowledge: auth/schema guidance общая или по реально существующим clients; historical facts не добавлять.
6. Final rg и report: перечислить сохранённые occurrences по категориям и причины. Assertions о текущем builtin проверить по `builtin-providers.json`, неизвестные live response/цены оставить неизвестными, код не менять.
7. После проверки заполнить «Отчёт» со ссылками на сверенные code/docs и оставшимися неизвестными; выполнить `scripts/release-task.sh 116` и `bash scripts/check.sh`, обновить PR по `docs/tasks.md`. Код приложения не менять.

### Проверка
```
rg -n "Replicate|Together" AGENTS.md docs -g '*.md'
bash scripts/check.sh
```

Файлы: `AGENTS.md`, `docs/product.md`, `docs/specs/image-client.md`, `docs/knowledge/csharp.md`

## Отчёт
Сверка выполнена по коду и документации; live-ответ Together не проверялся (ключа нет) — цена и форма ответа на живом сервисе остаются неизвестными (`docs/status.md`, «Открытые вопросы»).

Таблица вхождений и решения:

| Файл | Раздел | Категория | Решение |
|---|---|---|---|
| `AGENTS.md` | описание проекта | нормативное | заменено: встроенные fal.ai и OpenRouter + OpenAI-совместимые из конфига (Together AI выбран, ADR 0017, не встроен и не проверен) |
| `docs/product.md` | «Зачем нужен проект» | нормативное | заменено на то же |
| `docs/product.md` | «Решения владельца», Provider | нормативное | заменено: fal, OpenRouter и OpenAI-совместимые из конфига |
| `docs/product.md` | п. 8 «Добавление моделей через конфиг» | нормативное | заменено: F2.5 — модель fal по идентификатору; у OpenAI-совместимого провайдера модели из конфига |
| `docs/product.md` | «Термины» | нормативное | заменено: fal.ai, OpenRouter или OpenAI-совместимый провайдер из конфига |
| `docs/specs/image-client.md` | вступление (стр. 11) | нормативное | заменено |
| `docs/specs/image-client.md` | F1 (результат не теряется) | нормативное | Replicate-специфичный пример истечения заменён на fal (настраиваемый срок) |
| `docs/specs/image-client.md` | модерация провайдеров | нормативное | «safety checker у fal и Replicate» → только fal |
| `docs/specs/image-client.md` | F2 (параметры переводятся) | нормативное | источник возможностей: OpenRouter — ответ API, fal — схема входа, OpenAI-совместимый — `imageModels` из конфига (ADR 0009) |
| `docs/specs/image-client.md` | F2 (результат сохраняется сразу) | нормативное | заменено |
| `docs/specs/image-client.md` | «Один интерфейс» | нормативное | заменено |
| `docs/specs/image-client.md` | F1 (первая настройка) | нормативное | ключи встроенных fal и OpenRouter, дополнительные — через конфиг |
| `docs/specs/image-client.md` | F2.5 | нормативное | модель fal по идентификатору; у OpenAI-совместимого — модели из конфига, без чтения схемы |
| `docs/specs/image-client.md` | F7.2 | нормативное | заменено |
| `docs/specs/image-client.md` | макеты (строки 266, 271, 355, 377) | нормативное | Replicate → Together AI; «модель fal или Replicate» → «модель fal» |
| `docs/specs/image-client.md` | «Результат сохраняется локально» | нормативное | заменено |
| `docs/specs/image-client.md` | «Нефункциональные требования» (ссылки, частоты) | нормативное | заменено; Replicate-число из сторонних источников убрано |
| `docs/specs/image-client.md` | раздел «Как работают API провайдеров» | историческое исследование (27.10.2026) | сохранено; перед таблицей добавлена граница: это датированное исследование исходного набора, текущий выбор — Together AI (ADR 0017) |
| `docs/specs/image-client.md` | «Функции найденных приложений» | историческое исследование (что поддерживают другие приложения) | сохранено без правок |
| `docs/specs/image-client.md` | «Источники» | историческое исследование | сохранено; в абзац добавлена та же граница |
| `docs/knowledge/csharp.md` | «HTTP и провайдеры» (авторизация) | нормативное | Bearer — OpenRouter и OpenAI-совместимые провайдеры |
| `docs/knowledge/csharp.md` | «JSON» (динамические схемы) | нормативное | fal и OpenAI-совместимый провайдер |
| `docs/decisions/0003`, `0005`, `0009` | контекст решений | историческое | сохранено |
| `docs/log/*`, `docs/status.md` (решения владельца), `tasks/*` | журнал и решения | историческое | сохранено |
| `docs/status.md` | «Открытые вопросы» | нормативное | уже корректно: «Третий провайдер картинок — Together AI вместо Replicate (ADR 0017)» |

Отдельно, вне списка файлов плана: в `docs/specs/chat-sessions.md` строка «Не входит: fal и Replicate, Web и Docker» (граница скелета v0.1) называла несуществующего провайдера в нормативном перечне — заменено на «fal и Together AI». Файл не был указан в «Файлах» плана, но это настоящее противоречие ADR 0017, поэтому правка внесена и здесь описана.

Что не проверялось и остаётся неизвестным: живой ответ Together AI (цена, форма ответа, схема входа модели) — ключа у машины нет; встроенной записи Together в `builtin-providers.json` нет, её подключение — задача 136. Формулировки не обещают ни live-цен, ни schema-discovery у Together.

Проверено: `bash scripts/check.sh` проходит. Код приложения не менялся, сборка и тесты не требовались.

## Комментарии
