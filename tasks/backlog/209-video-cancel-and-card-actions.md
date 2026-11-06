---
id: 209
status: approved
assignee:
claimed:
created: 2026-11-06
---

# Видео: отмена, Repeat, New seed, Edit

## Описание
ADR 0027, п. 4. Действия над видео-генерацией в `GenerationService`. У OpenRouter нет отмены видео-задания, поэтому «Отмена» только перестаёт ждать.

Зависит от: 202.
Среда: Linux или Windows.

## Критерии готовности
- `CancelGenerationAsync` для `Kind = Video`: без вызова провайдера останавливает опрос, ставит `Cancelled`, `CompletedAt`, сохраняет и публикует; возвращает новый результат `GenerationCancelResult.StoppedWaiting` (добавить в перечисление); пишет Information «video job cannot be cancelled at the provider; stopped waiting». Если задание ещё не отправлено — как у картинок (`Cancelled`).
- `CancelRunAsync` по прогону видео — то же для каждой активной генерации.
- `RepeatAsync`, `NewSeedAsync`, `EditAsync` для видео-генерации создают новый видео-прогон через `StartVideoRunAsync` с той же моделью, `Run.VideoParameters` и `Run.FirstFrameGenerationId` исходного прогона (202).
- `RunOnModelAsync` для видео → `InvalidOperationException` («not supported for video»), до любых записей.
- Тесты.

## Анализ
`CancelGenerationCoreAsync` (ветки «до отправки», «после отправки», `CancelOutcome.*`), `RepeatAsync`/`NewSeedAsync`/`EditAsync`/`RunOnModelAsync` в `GenerationService`. Для видео нет `CancelAsync` у провайдера: ветка «после отправки» для видео — локальная остановка (`job.Source.Cancel()` и `PersistUserCancellationAsync`). `ChatPageModel.CancelCardAsync` помечает карточку только при `CancelFailed` — новый результат UI не ломает; предупреждение о возможной плате показывает 204.

## План
1. Падающие тесты в `GenerationServiceVideoTests`: отмена во время опроса → `StoppedWaiting`, статус `Cancelled`, провайдер больше не опрашивается; отмена прогона из двух видео; Repeat → новый прогон `Video` с той же моделью и параметрами; New seed → другой сид; Edit → новый промпт; Run on model → исключение, новых прогонов нет.
2. Реализация.

Файлы: `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core/Services/GenerationCancelResult.cs`, `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceVideoTests.cs`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
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

### 2026-11-06, claude-audit
Создано по поручению владельца («распланируй звук и видео»; выбрано: всё через OpenRouter, LibVLC в Avalonia сразу). ADR 0027, исследование — `docs/knowledge/media-generation.md`. План для Luna, ждёт одобрения.

### 2026-11-06, владелец (в чате, записал claude-audit)
План одобрен: «заапрувь все таски которые будут».
