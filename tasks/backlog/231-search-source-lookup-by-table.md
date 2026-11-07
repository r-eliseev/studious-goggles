---
id: 231
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-07T11:03:37Z
created: 2026-11-07
---

# Поиск: выборка источников пачки по индексам таблиц

## Описание
Находка ревью 2026-11-04 (задача 185): `SearchTextSource.ListSourcesByKeysAsync` фильтрует по вычисляемому ключу (`'title:' || id`, `'prompt:' || runs.id`, `'desc:' || image_descriptions.id`) поверх `UNION ALL` — SQLite не может использовать первичные ключи и для каждой пачки (64 ключа) просматривает все три таблицы с соединениями. При 10 000 источниках первая индексация — ~157 полных просмотров.

Зависит от: —
Среда: Linux или Windows.

## Критерии готовности
- Ключи пачки группируются по префиксу (`title:`, `prompt:`, `desc:`); для каждой непустой группы — отдельный запрос к своей таблице с `WHERE <таблица>.id IN (@k0, …)` (параметры, без подстановки строк), с тем же соединением с `sessions`, что в `SourceRowsQuery`; результаты объединяются. Ключи с неизвестным префиксом или нечитаемым id пропускаются.
- Результат совпадает с прежним на тех же данных (существующие тесты `SearchTextSourceTests` зелёные).
- Тест плана запроса: `EXPLAIN QUERY PLAN` запроса для `prompt:`-ключей содержит обращение к первичному ключу `runs` (строка с `USING` … `PRIMARY KEY` или `INTEGER PRIMARY KEY` — по фактическому выводу SQLite; если формат вывода нестабилен — проверять отсутствие `SCAN runs`).
- Тест: смешанная пачка из трёх видов ключей и одного неизвестного → три источника.

## Анализ
`ImageGen/ImageGen.Data/Repositories/SearchTextSource.cs` (`SourceRowsQuery`, `ListSourcesByKeysAsync`); тесты — `ImageGen/ImageGen.Data.Tests/Repositories/SearchTextSourceTests.cs`. Тип id в таблицах — `TEXT` (Guid строкой, `SqliteValues`); первичные ключи — в миграциях 0001, 0005.

## План
1. Падающие тесты (смешанная пачка, план запроса).
2. Группировка и три запроса.
3. Все тесты Data.

Файлы: `ImageGen/ImageGen.Data/Repositories/SearchTextSource.cs`, `ImageGen/ImageGen.Data.Tests/Repositories/SearchTextSourceTests.cs`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Data/ImageGen.Data.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

### Порядок исполнения (для Luna)
- Сначала прочитай только файлы из строки «Файлы» и названные тесты; сигнатуры сверяй с текущим `main`.
- Шаги строго по порядку (ub-mode): падающий тест → запуск, убедиться, что он падает именно по описанной причине → минимальный фикс → тест зелёный.
- Не меняй файлы вне строки «Файлы», не переименовывай существующие члены сверх плана, не чини соседние находки — они в отдельных задачах.
- Если код расходится с планом (нет такого метода, другая сигнатура, тест уже есть), не импровизируй: остановись и запиши вопрос в «Комментарии».
- Команды `dotnet` выполняй по одной, не параллельно. После каждого изменённого проекта — `dotnet format <проект> --verify-no-changes`.
- В «Отчёте»: что изменено, команды и результаты, отличия от плана.

## Отчёт

## Комментарии

### 2026-11-07, claude-audit
Создано по находке ревью 2026-11-04 (эффективность 185). План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
Поручение «на те проблемы, для которых нет задач, создай задачи»; план одобрен ранее данным поручением «заапрувь все таски которые будут».
