---
id: 185
status: approved
assignee:
claimed:
created: 2026-11-02
---

# Семантический индекс: не перечитывать все источники на каждую пачку

## Описание
Находка код-ревью 2026-11-02 (PR #70, задача 105). В цикле по пачкам (по 64) `IndexCurrentSourcesAsync` вызывает `_sources.ListSourcesAsync` и заново хэширует все тексты, чтобы проверить, что тексты пачки не изменились. При 10 000 источниках это 157 полных чтений и ~1,5 млн SHA-256 на первую индексацию; на Android — секунды CPU на каждую пачку.

Зависит от: —
Среда: Linux или Windows.

## Критерии готовности
- Новый метод `ISearchTextSource.ListSourcesByKeysAsync(IReadOnlyCollection<string> sourceKeys, CancellationToken)` возвращает только источники с этими ключами (отсутствующие ключи просто не возвращаются).
- В цикле по пачкам используется он, с ключами текущей пачки; `ListSourcesAsync` вызывается вне цикла фиксированное число раз, не зависящее от числа пачек.
- Хэш текста пачки считается один раз на источник.
- Поведение прежнее: удалённый или изменённый за время запроса источник не сохраняется (тест `IndexAsync_SourceDeletedDuringProviderCall_DoesNotPersistStaleVector` зелёный).

## Анализ
SQL источников — `SourcesQuery` в `ImageGen/ImageGen.Data/Repositories/SearchTextSource.cs` (UNION трёх таблиц с ключами `title:`, `prompt:`, `desc:`, `LIMIT 10001`). Новый запрос — тот же UNION без `ORDER BY`/`LIMIT`, обёрнутый во внешний `SELECT … WHERE source_key IN (@k0, @k1, …)`. Пачка ≤ 64 ключей, параметров хватает. SQL только с параметрами (AGENTS.md).

Реализации интерфейса: `SearchTextSource` (Data) и тестовый `MemorySources` в `SemanticIndexServiceTests.cs`. В остальных тестах интерфейс подменяется NSubstitute — им ничего не нужно.

## План
1. Data, падающий тест в `ImageGen/ImageGen.Data.Tests/Repositories/SearchTextSourceTests.cs`: `ListSourcesByKeysAsync_ReturnsOnlyRequestedExistingKeys` — сессия с прогоном и описанием; запрос с ключом заголовка, ключом промпта и несуществующим ключом → два источника с верными текстами. Пустой список ключей → пустой результат без запроса к базе.
2. Добавить метод в `ISearchTextSource` и реализовать в `SearchTextSource` (параметры `@k0..@kN` через цикл `AddWithValue`).
3. Core, падающий тест в `SemanticIndexServiceTests`: `IndexAsync_ManyBatches_DoesNotReloadAllSourcesPerBatch` — 200 источников (4 пачки), `MemorySources` считает вызовы `ListSourcesAsync`; ожидание: после индексации вызовов не больше, чем при одной пачке (запомнить число для 1 пачки в том же тесте или задать константой после чтения кода — не больше 3).
4. В `MemorySources` реализовать `ListSourcesByKeysAsync` фильтром по текущему списку и добавить счётчик вызовов `ListSourcesAsync`.
5. Фикс в `IndexCurrentSourcesAsync`: перед циклом для каждой пачки заранее посчитанный хэш (`HashText(source.Text)` один раз — например, словарь ключ → хэш для `pending`). В цикле вместо `ListSourcesAsync` — `ListSourcesByKeysAsync(ключи пачки)`; сравнивать хэш свежего текста с сохранённым хэшем.

Файлы: `ImageGen/ImageGen.Core/Search/ISearchTextSource.cs`, `ImageGen/ImageGen.Core/Services/SemanticIndexService.cs`, `ImageGen/ImageGen.Data/Repositories/SearchTextSource.cs`, `ImageGen/ImageGen.Data.Tests/Repositories/SearchTextSourceTests.cs`, `ImageGen/ImageGen.Core.Tests/Services/SemanticIndexServiceTests.cs`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
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

### 2026-11-02, claude-audit
Создано по код-ревью PR #67/#70 по поручению владельца; план для Luna. Ждёт одобрения.

### 2026-11-02, владелец (в чате, записал claude-audit)
План одобрен: «таски заапрувлены».
