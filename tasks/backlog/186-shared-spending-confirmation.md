---
id: 186
status: planned
assignee:
claimed:
created: 2026-11-02
---

# Одно подтверждение расходов для чата и выбора модели

## Описание
Находка код-ревью 2026-11-02 (PR #70, задача 021). Логика «оценка → `SpendingService.CheckAsync` → `ConfirmAsync`» написана дважды: `ChatPageModel.ConfirmEstimatedRunAsync` и `ModelPickerPageModel` (запуск на другой модели). Копии уже разошлись: в выборе модели сумма без `≈` и без «(+N unknown)», которые даёт `ChatPageModel.FormatEstimate`. Параметр `targetModel` в `ConfirmEstimatedRunAsync` всегда `null`.

Зависит от: 182 (формат цены; делать после её слияния).
Среда: Linux или Windows.

## Критерии готовности
- Один статический помощник в `ImageGen.Presentation/Spending/` выполняет оценку, проверку и диалог и возвращает «продолжать или нет»; оба экрана вызывают его.
- Форматирование оценки (`≈`, `$`, `0.####`, «(+N unknown)») — в одном месте, им пользуются и диалог, и строка «Next run» в чате.
- Неиспользуемый параметр `targetModel` удалён.
- Тексты в чате не меняются; в выборе модели появляются `≈` и «(+N unknown)».
- Конструкторы и регистрация DI не меняются.

## Анализ
Сейчас: `ChatPageModel.ConfirmEstimatedRunAsync(card, targetModel)`, `ChatPageModel.FormatEstimate(estimate)` (static), блок в `ModelPickerPageModel` перед `RunOnModelAsync`. Тесты: `ChatPageModelTests` (около строк 187, 821–838 — подтверждение/отказ), `ModelPickerPageModelTests` (использует `ImageGen.Presentation.Spending`). Строки ресурсов: `SpendingConfirmationFormat`, `CostEstimateWithUnknownFormat`, `SpendingWarningTitle`, `Generate`, `Cancel`.

## План
1. Падающий тест в `ModelPickerPageModelTests`: при оценке с `IsApproximate = true` и одной неизвестной моделью и сработавшем пороге текст диалога содержит `≈` и `(+1 unknown)` (проверять через `_dialogs.Received(1).ConfirmAsync(…, Arg.Is<string>(m => …), …)`).
2. Новый `static class SpendingConfirmation` в `ImageGen/ImageGen.Presentation/Spending/`:
   - `string FormatEstimate(CostEstimate estimate)` — перенести тело `ChatPageModel.FormatEstimate` без изменений.
   - `Task<bool> ConfirmAsync(CostEstimator estimator, SpendingService spending, IDialogService dialogs, IReadOnlyList<ModelRef> models, GenerationParameters parameters, CancellationToken ct)`: оценка с `runsPerModel = 1`; если `CheckAsync` вернул `None` → `true`; иначе диалог с `SpendingConfirmationFormat` и `FormatEstimate` → ответ пользователя.
3. `ChatPageModel`: `FormatEstimate` удалить, вызовы заменить на `SpendingConfirmation.FormatEstimate`; `ConfirmEstimatedRunAsync(card)` без `targetModel` — находит прогон как сейчас и вызывает `SpendingConfirmation.ConfirmAsync`.
4. `ModelPickerPageModel`: блок заменить вызовом `SpendingConfirmation.ConfirmAsync` с моделью `model.Reference` и параметрами исходного прогона (как сейчас); при `false` — `return`.
5. Все прежние тесты чата и выбора модели зелёные без правки ожиданий.

Файлы: `ImageGen/ImageGen.Presentation/Spending/SpendingConfirmation.cs` (новый), `ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/ModelPickerPageModel.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ModelPickerPageModelTests.cs`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
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
