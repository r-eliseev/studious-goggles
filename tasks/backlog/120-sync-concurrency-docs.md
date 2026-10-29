---
id: 120
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Уточнить документацию concurrency generation

## Описание
Architecture «не больше 4» не задаёт scope. После 111 это четыре локально отслеживаемых jobs на provider, включая fresh submit→terminal и resumed saved-handle polling; local cancellation освобождает slot, remote cancel failure остаётся limitation.

Зависит от: 111, 119.
Среда: Linux или Windows.

## Критерии готовности
- Architecture точно описывает per-provider local limit 4 и независимость providers.
- Fresh queued и recovered handle занимают slot полного tracked lifecycle.
- Local cancellation/failed remote cancel limitation названа без обещания строгого remote limit.
- ADR 0003 не переписывается; internal SemaphoreSlim не документируется.

## Анализ
Последняя из doc chain 117→119→120; перед работой rebase и сохранить обе предыдущие правки architecture.

## План
Для Haiku/Luna: одно contract-уточнение, no code/ADR.

1. Сверить 111 tests/helper и 121 limitation.
2. Заменить ambiguous phrase в architecture «Прогон»/«Ограничения» точным contract.
3. Упомянуть recovered watch и failed remote cancel одним коротким предложением.
4. Grep parallel/4 occurrences in active docs; исправить только contradictions.

### Проверка
```
grep -rn "не больше 4\|четыр.*одноврем\|параллел" docs/architecture.md docs/specs --include='*.md'
bash scripts/check.sh
```

Файлы: `docs/architecture.md`

## Отчёт

## Комментарии
