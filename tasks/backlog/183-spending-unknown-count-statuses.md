---
id: 183
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-02T17:52:57Z
created: 2026-11-02
---

# Расходы: «неизвестная цена» без неудачных и отменённых операций

## Описание
Находка код-ревью 2026-11-02 (PR #67, задача 021). `ExpenseRepository.CountUnknownAsync` считает все генерации с `cost_usd IS NULL`, в том числе `Failed`, `Cancelled`, `Queued`, `Running`. За них ничего не списано, но экран расходов пишет «неизвестная цена: N операций». Так же сообщения ассистента с ошибкой (`is_error = 1`) попадают в счёт.

Зависит от: —
Среда: Linux или Windows.

## Критерии готовности
- Генерации считаются только со статусом `Succeeded` (2) или `Filtered` (5) и `cost_usd IS NULL`.
- Сообщения ассистента считаются только при `is_error = 0`.
- Описания картинок и сводки — как сейчас.
- Тест в `ExpenseRepositoryTests` покрывает исключённые статусы и ошибочное сообщение.

## Анализ
Статусы хранятся числом (`GenerationStatus`: Queued 0, Running 1, Succeeded 2, Failed 3, Cancelled 4, Filtered 5). Допущение: `Filtered` учитывается, потому что провайдер мог взять плату за отфильтрованный ответ; если владелец решит иначе — убрать 5 из условия.

Существующий тест `CountUnknownAsync_CountsOnlyOperationsInsidePeriod` создаёт генерации через `TestData.Generation(run, 0)` со статусом по умолчанию `Queued`: после фикса он станет давать 0. Ему нужно явно передать `GenerationStatus.Succeeded` (третий параметр `TestData.Generation`).

## План
1. Падающий тест `CountUnknownAsync_IgnoresOperationsThatCannotBeCharged`: один прогон в периоде с генерациями `Succeeded`, `Filtered`, `Failed`, `Cancelled`, `Queued`, `Running` (все без цены). Ожидать 2. Если в тестовых данных есть удобный способ добавить сообщение ассистента (`ChatMessageRepository`, `TestData`), добавить ещё сообщение с `IsError = true` без цены и ожидание не меняется; если нет — проверить сообщение отдельным маленьким тестом на SQL через `ChatMessageRepository.AddAsync` (найти точное имя метода в репозитории).
2. В существующем тесте передать `GenerationStatus.Succeeded` в обе `TestData.Generation`.
3. Фикс SQL в `CountUnknownAsync`: в подзапрос `generations` добавить `AND status IN (2, 5)`, в подзапрос `chat_messages` — `AND is_error = 0`. Числа не подставлять строкой из C#: они литералы в тексте запроса.

Файлы: `ImageGen/ImageGen.Data/Repositories/ExpenseRepository.cs`, `ImageGen/ImageGen.Data.Tests/Repositories/ExpenseRepositoryTests.cs`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
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
