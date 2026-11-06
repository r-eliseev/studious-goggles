---
id: 202
status: planned
assignee:
claimed:
created: 2026-11-06
---

# GenerationService: запуск, ожидание и сохранение видео

## Описание
ADR 0027, пп. 3, 4. `GenerationService` запускает видео-прогон по моделям сессии, отправляет задания через `IQueuedVideoProvider` (201), ждёт результата с опросом, сразу скачивает файл потоком в хранилище (199) и восстанавливает незавершённые задания после перезапуска. Отмена и действия карточки — 209; настройки сессии и инструмент ассистента — 203.

Зависит от: 199, 200, 201.
Среда: Linux или Windows.

## Критерии готовности
- `VideoParameters` (record в `ImageGen.Core/Domain/Sessions/`): `int DurationSeconds` (по умолчанию 5), `string? Resolution`, `string? AspectRatio`, `bool GenerateAudio` (по умолчанию `true`); `static VideoParameters Default`.
- `Run.VideoParameters` (`VideoParameters?`, у картинок `null`) и `Run.FirstFrameGenerationId` (`Guid?`); миграция `NNNN_run_video_parameters.sql` (`runs.video_parameters_json TEXT NULL`, `runs.first_frame_generation_id TEXT NULL`); `RunRepository` пишет и читает оба поля (JSON — по образцу `ParameterJson.Run`).
- `GenerationService.StartVideoRunAsync(Guid sessionId, string prompt, IReadOnlyList<ModelRef> models, VideoParameters parameters, Guid? firstFrameGenerationId, CancellationToken)` → `Run`: по одной генерации `Kind = Video` на модель (без «запусков на модель»), `DurationSeconds = parameters.DurationSeconds`, сид — как у картинок (общий `ISeedSource`), если модель его поддерживает (`VideoModel.SupportsSeed == true`), иначе `SeedApplied = false`. Пустой список моделей → `ArgumentException`. Первый кадр: читается файл успешной картинки этой сессии (`IImageFileStore.ReadAsync`), иначе `ArgumentException`.
- Выполнение видео-генерации: `Queued` → отправка (`SubmitAsync`) → `JobJson` и `Running` сохраняются → опрос с интервалом от 5 до 30 с (новые константы рядом с `PollMinInterval`/`PollMaxInterval`) → при `Succeeded`: `OpenContentAsync` → `SaveMediaStreamAsync(..., ProviderResponseLimits.Video)` → `ImagePath`, `MediaType`, `CostUsd`, `CompletedAt`, `Succeeded`; при `Failed` — `Failed` с видом и текстом ошибки. Каждое изменение сохраняется и публикуется событием, как у картинок. Ошибка скачивания/сохранения → `Failed`, временного файла нет.
- `RecoverInterruptedAsync`: генерации `Kind = Video` в `Queued`/`Running` с `JobJson` продолжают опрос; без `JobJson` — `Failed` (как картинки без задания).
- Слот провайдера держится только на время отправки (как у картинок); опрос идёт без слота.
- Тесты в стиле `GenerationServiceTests` (подставной `IQueuedVideoProvider`, `RecordingAsyncDelay`).

## Анализ
Картинки: `StartRunAsync` (создание прогона и генераций, сиды, `AddRunAsync`), `Launch`/`RunWithProviderSlotAsync`, `ExecuteAsync` → `ExecuteQueuedAsync` → `SubmitQueuedAsync` + `WatchQueuedAsync` (опрос, `StateGate`, `ApplyUpdateAsync`, сохранение картинки), `RecoverInterruptedAsync` (ветка с `JobJson`). Видео повторяет эту машину состояний с другим провайдером и сохранением. Чтобы не плодить копии, допустимо вынести общий цикл опроса в метод с делегатами «опросить» и «применить результат»; если это требует менять поведение картинок — не выносить, а написать отдельные методы `ExecuteVideoAsync`/`WatchVideoAsync` и записать решение в «Отчёт».

Каталог видео — `VideoModelCatalogService.FindAsync` (200) для проверки поддержки сида; если модели нет в каталоге — сид не применяется.

## План
1. Падающие тесты (`GenerationServiceVideoTests.cs`, общий набор подставных объектов — по образцу `GenerationServiceTests`):
   - две модели → прогон с двумя генерациями `Video`, `Run.VideoParameters` сохранён, `DurationSeconds` = 5;
   - задание: `Queued` → после отправки `JobJson` записан и статус `Running` → опрос `Running`, затем `Succeeded` с ценой 0.4 → файл сохранён через `SaveMediaStreamAsync` с лимитом видео, генерация `Succeeded`, `CostUsd` = 0.4, событие опубликовано;
   - `Failed` от провайдера → `Failed` с текстом;
   - `SaveMediaStreamAsync` бросает «слишком большой» → `Failed`;
   - перезапуск: генерация `Running` с `JobJson` → после `RecoverInterruptedAsync` опрос продолжается и завершается `Succeeded`;
   - первый кадр: id картинки чужой сессии → `ArgumentException`, задание не отправлено;
   - интервалы опроса через `RecordingAsyncDelay`: первый 5 с, не больше 30 с.
2. Data: миграция и `RunRepository` (тест чтения/записи `VideoParameters` и `FirstFrameGenerationId`).
3. Реализация в `GenerationService`.

Файлы: `ImageGen/ImageGen.Core/Domain/Sessions/VideoParameters.cs` (новый), `ImageGen/ImageGen.Core/Domain/Generations/Run.cs`, `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Data/Migrations/NNNN_run_video_parameters.sql` (новый), `ImageGen/ImageGen.Data/Repositories/RunRepository.cs`, `ImageGen/ImageGen.Data/ParameterJson.cs` (или где лежит `ParameterJson`; `rg -n "class ParameterJson" ImageGen`), `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceVideoTests.cs` (новый), `ImageGen/ImageGen.Data.Tests/Repositories/RunRepositoryTests.cs`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data/ImageGen.Data.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
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

### 2026-11-06, claude-audit
Создано по поручению владельца («распланируй звук и видео»; выбрано: всё через OpenRouter, LibVLC в Avalonia сразу). ADR 0027, исследование — `docs/knowledge/media-generation.md`. План для Luna, ждёт одобрения.
