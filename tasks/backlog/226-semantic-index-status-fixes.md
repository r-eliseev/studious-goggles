---
id: 226
status: approved
assignee:
claimed:
created: 2026-11-07
---

# Семантический индекс: статус не застревает (повторное включение, возврат ключа)

## Описание
Находки ревью 2026-11-04 (PR #72/#73), не исправлены (код на `ce90792`, `SemanticIndexService.GetStatusAsync`). Станут заметны, как только индексирование заработает (217), и мешают смене модели (227).
1. Ветка `else if (_indexTask is { IsCompleted: true } && _status.State == SemanticIndexState.Ready)` (≈строка 115) стоит раньше `else if (!_hasStarted && !_paused)`: `EnableAsync` (сбрасывает `_hasStarted`, чтобы начать заново) при статусе Ready индексацию не запускает, а счётчики `Indexed`/`Pending` после первого прогона больше не пересчитываются.
2. После удаления ключа OpenRouter статус `Unavailable/missing_key`; после возврата ключа ветка `else if (_status.State is Failed or Unavailable)` (≈128) сохраняет старое состояние, потому что `_hasStarted` остался `true`: «нет ключа» висит до выключения/включения поиска.

Зависит от: 217 (тот же файл; делать после её слияния).
Среда: Linux или Windows.

## Критерии готовности
- `EnableAsync` при любом прежнем состоянии запускает индексацию (если включено и есть ключ).
- `GetStatusAsync` при завершённой индексации возвращает свежие счётчики из хранилища (а не кэш статуса), сохраняя состояние `Ready`; новые источники без события сессии дают `Pending > 0`, и индексация запускается (как при первом вызове).
- Ключ появился после `missing_key` → индексация запускается, статус уходит из `Unavailable`.
- `Failed` с другой причиной (не `missing_key`) остаётся `Failed` до явного повтора (кнопка «Retry»), как сейчас.
- Тесты в `SemanticIndexServiceTests`: (а) Ready → `EnableAsync` → провайдер вызван снова для новых источников; (б) Ready, добавлен источник без события → `GetStatusAsync` даёт `Pending = 1` и запускает индексацию; (в) нет ключа → `missing_key`; ключ добавлен → `GetStatusAsync` запускает индексацию, статус `Indexing`/`Ready`; (г) `Failed` по ошибке провайдера остаётся `Failed` при `GetStatusAsync`.

## Анализ
`ImageGen/ImageGen.Core/Services/SemanticIndexService.cs`: `GetStatusAsync` (порядок веток под `lock (_gate)`), `EnableAsync`, `StartIndexingLocked`, поля `_hasStarted`, `_paused`, `_status`. Тесты — `ImageGen/ImageGen.Core.Tests/Services/SemanticIndexServiceTests.cs` (подставные источники `MemorySources`, ключи — по существующим тестам `missing_key`).

## План
1. Падающие тесты (а)–(г).
2. Порядок веток: (1) идёт индексация → `Indexing`; (2) `missing_key` в прошлом статусе, а ключ есть → сброс `_hasStarted` и запуск; (3) `!_hasStarted && !_paused` → запуск; (4) `Failed`/`Unavailable` (кроме случая 2) → прежнее состояние; (5) завершено → свежие счётчики, `Ready`, при `Pending > 0` — запуск.
3. Прогнать все тесты службы.

Файлы: `ImageGen/ImageGen.Core/Services/SemanticIndexService.cs`, `ImageGen/ImageGen.Core.Tests/Services/SemanticIndexServiceTests.cs`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
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
Создано по находкам ревью 2026-11-04 вместе с выбором модели эмбеддингов (227) по поручению владельца. План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
План одобрен ранее данным поручением «заапрувь все таски которые будут».
