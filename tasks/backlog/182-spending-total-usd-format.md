---
id: 182
status: approved
assignee:
claimed:
created: 2026-11-02
---

# Экран расходов: итог в долларах, а не в валюте системы

## Описание
Находка код-ревью 2026-11-02 (PR #70, задача 021). `SpendingPageModel.RefreshAsync`: `TotalText = rows.Sum(...).ToString("C", CultureInfo.CurrentCulture)`. Суммы в журнале — USD, а формат `C` берёт валюту системы: в русской локали `0,03 ₽`, и всё меньше цента округляется до нуля. В остальном приложении цена пишется как `$` + `0.####` (`GenerationItemViewModel.CostTextOf`, `ChatPageModel.FormatEstimate`).

Зависит от: —
Среда: Linux или Windows.

## Критерии готовности
- `TotalText` = `$` + сумма в формате `0.####` с `InvariantCulture`, через строку `AppResources.CostFormat` (как в `CostTextOf`).
- Ноль показывается как `$0`.
- Тест при `CurrentCulture = ru-RU`: сумма 0.0345 → `$0.0345`.

## Анализ
`AppResources.CostFormat` = `${0}`. Тесты экрана: `ImageGen/ImageGen.Presentation.Tests/Spending/SpendingPageModelTests.cs`. Для смены культуры в тесте сохранить `CultureInfo.CurrentCulture`, поставить `ru-RU`, вернуть в `finally`.

Группы (`SpendingGroup.TotalUsd`) — число, их форматирует разметка; разметку не трогать.

## План
1. Падающий тест `RefreshAsync_TotalUsesDollarFormatIndependentOfCulture`: журнал с двумя записями 0.03 и 0.0045 в текущем месяце (по образцу существующего теста `RefreshAsync_MonthPeriodGroupsByProviderAndModelAndCountsUnknown`), культура ru-RU, ожидание `TotalText == "$0.0345"`.
2. Фикс: в `RefreshAsync` сформировать текст так же, как `CostTextOf`: `sum.ToString("0.####", InvariantCulture)` → `string.Format(InvariantCulture, AppResources.CostFormat, price)`. Без нового общего хелпера (дублирование форматирования убирает задача 186).

Файлы: `ImageGen/ImageGen.Presentation/PageModels/SpendingPageModel.cs`, `ImageGen/ImageGen.Presentation.Tests/Spending/SpendingPageModelTests.cs`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
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
