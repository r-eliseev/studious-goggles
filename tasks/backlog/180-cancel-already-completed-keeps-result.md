---
id: 180
status: approved
assignee:
claimed:
created: 2026-11-02
---

# Отмена: ответ «уже завершено» не должен выбрасывать готовую картинку

## Описание
Находка код-ревью 2026-11-02 (PR #70, задача 024). `GenerationService.CancelGenerationCoreAsync`: при `CancelOutcome.AlreadyCompleted` код отменяет наблюдение (`job.Source`) и сохраняет `Cancelled`. Но `CancelOutcome.AlreadyCompleted` по своему описанию значит «задание завершилось до отмены; результат даст наблюдение». Провайдер уже взял деньги, картинка не скачивается, расход не попадает в журнал. Нарушено правило AGENTS.md «Результат генерации сохраняется сразу при получении». fal.ai отвечает так (HTTP 400), когда пользователь нажимает «Отмена» в момент завершения.

Зависит от: —
Среда: Linux или Windows.

## Критерии готовности
- `AlreadyCompleted`: наблюдение не отменяется, статус не меняется на `Cancelled`; результат приходит обычным путём (`Succeeded`/`Failed`/`Filtered`), картинка и расход сохраняются.
- `CancelGenerationAsync` в этом случае возвращает `GenerationCancelResult.NotActive` (задание уже не отменить), карточка не помечается «отмена не удалась».
- `NotFound` и `Cancelled` работают как сейчас (`Cancelled`).
- План задачи 024 и `docs/specs/` (если там описана отмена) приведены в соответствие.

## Анализ
Код: блок `if (outcome is CancelOutcome.AlreadyCompleted or CancelOutcome.NotFound)` в `CancelGenerationCoreAsync`. Существующий тест `CancelGenerationAsync_MapsProviderOutcomeAndPersistsState` (теория, `GenerationServiceTests.cs`, ~строка 44) ожидает для `AlreadyCompleted` результат `Cancelled` — эту строку данных нужно изменить. Тест со «save в процессе» (около строки 180, `AlreadyCompleted` + `NotActive` + `Succeeded`) должен остаться зелёным.

Отмена при остановке сессии (`StopSessionAsync_…RemotelyCancelsBeforeStopCompletes`) — другой путь, его не трогать.

`ChatPageModel.CancelCardAsync` помечает карточку только при `CancelFailed`, поэтому `NotActive` в UI ничего не ломает.

## План
1. Тест (падающий): новый `CancelGenerationAsync_AlreadyCompleted_KeepsWatchingAndSavesResult`.
   - Как в существующей теории: `PollAsync` ждёт `pollResult`; `CancelAsync` → `AlreadyCompleted`.
   - Вызвать `CancelGenerationAsync`, ожидать `NotActive`.
   - Затем `pollResult.SetResult(JobUpdate Succeeded с картинкой и CostUsd 0.014)`, `await _service.WhenIdleAsync()`.
   - Ожидать: статус `Succeeded`, `ImagePath` задан, `CostUsd` = 0.014, `_files.SaveAsync` вызван один раз.
2. В теории `CancelGenerationAsync_MapsProviderOutcomeAndPersistsState` убрать строку `InlineData(AlreadyCompleted, Cancelled)` (её покрывает новый тест).
3. Фикс: разделить ветку. `NotFound` — как сейчас. `AlreadyCompleted` — без захвата `StateGate`, без `job.Source.Cancel`, без смены статуса; записать `LogInformation` («provider reports generation already completed; keeping watch») и вернуть `NotActive`.
4. В `CancelOutcome.cs` описание не менять (оно уже правильное). В `tasks/backlog/024-cancel-generation.md`, план, п. про `Cancelled/AlreadyCompleted/NotFound`, дописать в «Комментарии» строку: `AlreadyCompleted` исправлено задачей 180. В `docs/specs/` найти описание отмены (`rg -n "AlreadyCompleted|отмен" docs/specs`) и, если там сказано «AlreadyCompleted → Cancelled», поправить.

Файлы: `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`, `tasks/backlog/024-cancel-generation.md`, при необходимости `docs/specs/image-client.md`.

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

### 2026-11-02, claude-audit
Создано по код-ревью PR #67/#70 по поручению владельца; план для Luna. Ждёт одобрения.

### 2026-11-02, владелец (в чате, записал claude-audit)
План одобрен: «таски заапрувлены».
