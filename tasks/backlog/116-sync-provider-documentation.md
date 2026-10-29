---
id: 116
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Заменить Replicate на Together AI в активной документации

## Описание
Найдено ревью 2026-10-29. ADR 0017 принят и выбирает Together AI вместо Replicate, подключаемый как OpenAI-compatible provider. Активные документы `AGENTS.md`, `docs/product.md` и `docs/specs/image-client.md` продолжают обещать Replicate и отдельную asynchronous prediction integration. Исторические ADR, task reports и logs переписывать запрещено.

Зависит от: —
Среда: Linux или Windows.

## Критерии готовности
- `AGENTS.md` описывает текущих встроенных провайдеров OpenRouter/fal и Together AI как настраиваемый OpenAI-compatible provider.
- `docs/product.md` и `docs/specs/image-client.md` не называют Replicate частью актуальной цели/терминов/API-таблицы.
- Возможности Together AI не выдуманы: непроверенные цена и live response остаются явно непроверенными со ссылкой на ADR 0017/status.
- Исторические `docs/decisions`, `docs/log`, `tasks` и `poc` не переписаны.
- `grep Replicate` после правки выдаёт только исторические документы или явно помеченный контекст замены.

## Анализ
- Реализация не содержит Replicate client.
- Together AI использует существующий `OpenAiCompatibleClient` и `providers.json`, отдельного `ProviderKind` не нужно.
- `docs/specs/image-client.md` содержит таблицу API, moderation examples и F2.5, связанные с Replicate; каждое место нужно либо заменить подтверждённым Together поведением, либо убрать неподтверждённую конкретику.
- `docs/knowledge/csharp.md` также содержит активные Replicate-specific примеры аутентификации и dynamic schema; это не исторический документ.

## План
Для исполнителя (Haiku/Luna): задача только про документацию. Не меняй код, ADR, task reports и logs. Не ищи новые факты в интернете: используй только ADR 0017 и текущую реализацию; неизвестное называй неизвестным.

Прочитай: ADR 0017, ADR 0009, `ProviderKind.cs`, `ProviderConfigLoader.cs`, `AGENTS.md`, `docs/product.md`, `docs/specs/image-client.md`, открытые вопросы `docs/status.md`.

1. Составить список всех `Replicate` через grep и разделить на active docs и historical records. Записать список active docs в отчёт.
2. Обновить вводный абзац и термины `AGENTS.md`: OpenRouter и fal встроены; Together AI подключается через `providers.json` как OpenAI-compatible.
3. В `docs/product.md` заменить текущую тройку и схему Provider. Не утверждать, что Together key «подключается один раз» через встроенный экран, если пользовательский provider ещё требует конфиг.
4. В `docs/specs/image-client.md` обновить цель, provider/API overview, F2.5 и примеры. Удалить Replicate-specific срок ссылок, prediction state и safety switch, если для Together нет подтверждения. Сохранить общие продуктовые требования.
5. В `docs/knowledge/csharp.md` убрать Replicate-specific active guidance; оставить общие примеры HTTP/auth/schema либо заменить только фактами, подтверждёнными существующим OpenAI-compatible client.
6. Не менять исторический текст ADR 0003; его заголовок/контекст остаётся записью решения того времени. ADR 0017 уже фиксирует замену.
7. Запустить grep повторно и `scripts/check.sh`.

### Проверка
```
grep -rn "Replicate" AGENTS.md docs --include='*.md'
bash scripts/check.sh
```

Файлы: `AGENTS.md`, `docs/product.md`, `docs/specs/image-client.md`, `docs/knowledge/csharp.md`

## Отчёт

## Комментарии
