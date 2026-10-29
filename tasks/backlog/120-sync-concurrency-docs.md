---
id: 120
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Уточнить документацию лимита генераций

## Описание
Найдено ревью 2026-10-29. Architecture говорит «не больше 4 одновременно» без области действия, а ADR 0003 требует семафор на provider. После задачи 111 лимит действует отдельно для каждого provider и удерживается queued job на всём lifecycle submit→terminal.

Зависит от: 111.
Среда: Linux или Windows.

## Критерии готовности
- Architecture явно говорит: максимум четыре активных generation jobs на каждый provider.
- Для queued provider активность считается от начала submit до terminal state, включая polling.
- Разные providers не блокируют друг друга своим лимитом.
- Документ совпадает с константой и реализацией `GenerationService` после 111.

## Анализ
- Старый глобальный `_slots` и освобождение после submit исправляет 111.
- ADR 0003 менять не нужно: его решение уже требует per-provider semaphore.
- Задача отделена от 117/119, потому что меняет одно предложение и имеет отдельную code dependency.

## План
Для исполнителя (Haiku/Luna): задача только документационная. Не менять код и ADR. Если 111 реализована иначе, поставить `blocked` и привести точные строки кода.

Прочитай: ADR 0003, `GenerationService` после 111, `docs/architecture.md` paragraph «Прогон» и «Ограничения».

1. Заменить неоднозначное «не больше 4 одновременно» точным per-provider contract.
2. Одним предложением уточнить queued lifecycle и независимость providers; не описывать внутренний `SemaphoreSlim`.
3. Grep всех чисел/формулировок лимита в active docs и устранить противоречия только там.

### Проверка
```
grep -rn "не больше 4\|четыр.*одноврем\|параллел" docs/architecture.md docs/specs --include='*.md'
bash scripts/check.sh
```

Файлы: `docs/architecture.md`

## Отчёт

## Комментарии
