---
id: 035
status: done
assignee:
claimed:
created: 2026-10-27
---

# Миграции: брать скрипты из папки, а не из списка

## Описание
Заменить явный список миграций в `ImageGen/ImageGen.Data/Migrations.cs` на чтение всех встроенных `Migrations/*.sql`, отсортированных по имени. Новая миграция — просто новый файл `NNNN_имя.sql` без правки кода.

Источник: ревью PR #1, комментарий к `Migrations.cs` (строка 9).

Зависит от: — (первая в очереди).
Среда: Linux или Windows (только `ImageGen.Data` и его тесты).

## Критерии готовности
- Новая миграция добавляется одним файлом `Migrations/NNNN_имя.sql`, без правки C#-кода.
- Ошибки в наборе миграций (пропуск номера, повтор номера, неверное имя файла) обнаруживаются при старте с понятным текстом, а не молча.
- Существующая база с `user_version = 1` открывается без изменений; новая база получает все миграции.
- Тест-кейсы из плана проходят вместе с остальными тестами `ImageGen.Data.Tests`.

## Анализ
- Сейчас: `ImageGen.Data.csproj` встраивает `Migrations/*.sql` с `LogicalName="%(Filename)%(Extension)"`, то есть имя ресурса — просто имя файла (`0001_initial.sql`). `Migrations.All` — явный список вызовов `ReadScript("0001_initial.sql")`.
- `SqliteDatabase.InitializeAsync` применяет `Migrations.All[index]` для `index` от `user_version` до `Count − 1`, каждую в своей транзакции, и ставит `user_version = index + 1`. Значит, номер миграции обязан совпадать с её позицией: `0001` — первая, `0002` — вторая. Пропуск номера сдвинул бы версии, поэтому набор нужно проверять.
- Если `user_version` больше числа миграций (база от более новой версии приложения), сейчас ничего не происходит и приложение работает со схемой, которую не знает. План делает это явной ошибкой: откат версии приложения не поддерживается (ADR 0002 описывает только движение вперёд).
- Задача идёт первой в порядке backlog (решение владельца 2026-10-28): после неё 036, 020, 018, 019, 025, 027 добавляют миграции одним файлом `NNNN_<смысл>.sql` (правило в `docs/tasks.md`).
- Пересечений по файлам с другими задачами нет: `Migrations.cs`, `ImageGen.Data.csproj` и тесты `SqliteDatabaseTests` другие планы не меняют, они только добавляют файлы `.sql`.
- `InternalsVisibleTo` для `ImageGen.Data.Tests` уже есть (по нему тестируется `FileImageStore.GetExtension`), поэтому `Migrations` остаётся `internal`.

## План
1. `ImageGen.Data.csproj`: имя ресурса получает префикс, чтобы отличать миграции от любых будущих ресурсов: `LogicalName="Migrations.%(Filename)%(Extension)"` (например `Migrations.0001_initial.sql`).
2. `Migrations.cs`:
   - Константа префикса `Migrations.` и шаблон имени `^\d{4}_[a-z0-9_]+\.sql$` (после префикса).
   - `Load(IEnumerable<(string Name, string Script)>) → IReadOnlyList<Migration>` — чистая функция, тестируется без сборки:
     ```
     для каждого (name, script):
         имя не по шаблону → ошибка «неверное имя миграции <name>»
         number = первые 4 цифры
     отсортировать по number (затем по имени — для сообщения об ошибке)
     повтор number → ошибка «два файла миграции с номером NNNN: <a>, <b>»
     ожидаемый номер = 1, 2, 3, … ; расхождение → ошибка «пропущена миграция NNNN»
     пустой набор → ошибка «нет ни одной миграции»
     вернуть [Migration(Number, Name, Script)] по порядку
     ```
   - `Migration` — запись `(int Number, string Name, string Script)`.
   - `All` — ленивое значение: ресурсы сборки с префиксом `Migrations.` → `(имя без префикса, текст)` → `Load`. Ошибка `Load` — `InvalidOperationException`; она возникает при первой инициализации базы, то есть при старте приложения.
3. `SqliteDatabase.InitializeAsync`:
   - Применяет `Migrations.All[i].Script` как сейчас; `user_version` после миграции = её `Number` (а не `index + 1`; при проверенном наборе это одно и то же, но связь становится явной).
   - Новое: `user_version > Migrations.All.Count` → `InvalidOperationException` «база создана более новой версией приложения (версия схемы N, известно M)»; база не меняется.
4. Документация: в `docs/architecture.md`, раздел «Скелет v0.1: хранение», фраза «подключаются через `Migrations.cs`» уточняется: «все встроенные `Migrations/NNNN_*.sql` по номеру, без списка в коде». ADR не нужен: решение ADR 0002 (свои миграции по `PRAGMA user_version`) не меняется.

### Крайние случаи
- Файл `.sql` положили в папку, но не по шаблону (`2_fix.sql`, `0002-Fix.sql`) → ошибка при старте с именем файла.
- Две ветки добавили миграцию с одним номером → после мерджа ошибка «два файла с номером»; лечится перенумерацией одной из них (правило `docs/tasks.md`).
- Пропуск номера (`0001`, `0003`) → ошибка «пропущена миграция 0002».
- Существующая база `user_version = 1`, в сборке только `0001` → ничего не применяется (как сейчас).
- База `user_version = 3`, в сборке миграции до `0002` → ошибка, база не меняется.

### Тест-кейсы (условие → результат)
`ImageGen.Data.Tests/MigrationsTests.cs` (для `Migrations.Load`, без БД):
1. `0002_b.sql`, `0001_a.sql` → порядок `0001_a`, `0002_b`; номера 1 и 2.
2. `0001_a.sql`, `0003_c.sql` → `InvalidOperationException`, в тексте `0002`.
3. `0001_a.sql`, `0001_b.sql` → исключение, в тексте оба имени.
4. `1_a.sql` → исключение «неверное имя», в тексте имя.
5. `0001-A.sql` → исключение «неверное имя».
6. Пустой набор → исключение.
7. Текст скрипта возвращается без изменений.

`Migrations.All` (встроенные ресурсы):
8. Содержит `0001_initial` с номером 1; число элементов равно числу файлов `Migrations/*.sql` в проекте.

`SqliteDatabaseTests` (временная БД, как сейчас):
9. Новая база → `user_version` равен номеру последней миграции (существующий тест `InitializeAsync_NewFile_SetsSchemaVersionToMigrationCount` остаётся и проходит).
10. База с `user_version`, большим числа миграций (выставить `PRAGMA user_version` вручную) → `InvalidOperationException`; `user_version` после вызова не изменился.
11. Повторный вызов на актуальной базе → ничего не применяется (существующий тест `InitializeAsync_CalledTwice_DoesNotFail`).

### Команды проверки
```
dotnet format ImageGen/ImageGen.Data/ImageGen.Data.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj --verify-no-changes
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Data/ImageGen.Data.csproj`, `ImageGen/ImageGen.Data/Migrations.cs`, `ImageGen/ImageGen.Data/SqliteDatabase.cs`, `ImageGen/ImageGen.Data.Tests/MigrationsTests.cs` (новый), `ImageGen/ImageGen.Data.Tests/SqliteDatabaseTests.cs`, `docs/architecture.md`

## Отчёт
- Изменено: `ImageGen.Data.csproj` (префикс `Migrations.` у имён ресурсов), `Migrations.cs` (`Load` и ленивый `All` из ресурсов), `Migration.cs` (новый, запись `Migration`), `SqliteDatabase.cs` (версия по `Migration.Number`, проверка «база новее приложения»), `docs/architecture.md` (одна фраза про миграции).
- Тесты: `ImageGen.Data.Tests/MigrationsTests.cs` (новый, 9 тестов: порядок, пропуск номера, повтор номера, неверное имя, пустой набор, текст скрипта, `All`), `SqliteDatabaseTests.cs` (+1: база с `user_version` больше числа миграций).
- Порядок TDD: сначала тесты (сборка падала на `Migrations.Load`), затем код.
- Результаты: `dotnet test ImageGen.Data.Tests` — 48/48 (было 38/38). `dotnet format --verify-no-changes` для `ImageGen.Data` и `ImageGen.Data.Tests` — код 0.
- Отклонение: в `SqliteDatabase.InitializeAsync` проверка версии стоит до `PRAGMA journal_mode = WAL`, чтобы слишком новая база не менялась даже в режиме журнала (план такого порядка не фиксировал).
- Миграции схемы: новых и изменённых нет (только `0001_initial.sql`, он без изменений).
- Не проверено отдельным тестом: добавление `NNNN_имя.sql` без правки кода; обеспечено маской `Migrations/*.sql` в csproj.
- Среда: Linux (облачная сессия). Windows-проверок у задачи нет.

## Комментарии

### 2026-10-28, ревью (claude, Windows)
План написан по просьбе владельца, статус `planned`. Одно решение сверх описания задачи: база от более новой версии приложения (`user_version` больше числа миграций) становится ошибкой при старте, а не молча открывается. Если владелец против, пункт 3 «Новое» и тест 10 убираются.

### 2026-10-28, владелец (в чате, записал ревью)
План одобрен как есть, включая ошибку при старте на базу от более новой версии приложения (пункт 3 «Новое», тест 10).
