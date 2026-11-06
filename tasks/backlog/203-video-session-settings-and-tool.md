---
id: 203
status: approved
assignee:
claimed:
created: 2026-11-06
---

# Видео в сессии: модели, параметры, инструмент ассистента, оценка цены

## Описание
ADR 0027, п. 6. Сессия хранит видео-модели и параметры видео; ассистент получает инструмент `generate_video` и запускает видео-прогон (202); оценка цены и предупреждения о расходах (021) учитывают видео. UI настроек — 204.

Зависит от: 202.
Среда: Linux или Windows.

## Критерии готовности
- `SessionSettings.VideoModels` (`List<ModelRef>`, пусто по умолчанию) и `SessionSettings.VideoParameters` (`VideoParameters.Default`); сохраняются в `settings_json` без миграции; старые сессии читаются с пустым списком и параметрами по умолчанию.
- `SessionService.UpdateVideoSettingsAsync(sessionId, IReadOnlyList<ModelRef> models, VideoParameters parameters, ct)`: модели проверяются по `VideoModelCatalogService` (неизвестная → `ArgumentException`), длительность — из `supported_durations` каждой модели (не поддерживаемая хотя бы одной моделью → `ArgumentException`).
- Инструмент `generate_video` (`AssistantPrompts.GenerateVideoToolName`, описание, JSON-схема: `prompt` — строка, обязательный; `first_frame_image_id` — строка, необязательный: id успешной картинки этой сессии) добавляется в запрос ассистента, только если у сессии есть видео-модели. Обработка ответа: `generate_video` → `GenerationService.StartVideoRunAsync(session.Settings.VideoModels, session.Settings.VideoParameters, firstFrame)`; неверные аргументы → `ErrorKind.InvalidToolCall`, как у картинок. Если в ответе оба инструмента — выполняется первый по порядку, второй игнорируется с Warning.
- Описание инструмента и системный промпт сообщают ассистенту, когда звать видео (пользователь явно просит видео/ролик/анимацию), а когда картинки; текст — по образцу `GenerateImagesToolDescription`, без изменения поведения картинок.
- `CostEstimator.EstimateVideoAsync(models, VideoParameters, ct)` → `CostEstimate` через `VideoPriceCalculator` (200); неизвестная цена → модель в `UnknownModels`; `IsApproximate = false`.
- Тесты Core.

## Анализ
- `AssistantService`: построение запроса (`BuildRequestAsync`, `new AssistantTool(GenerateImagesToolName, …)`), разбор ответа (`reply.ToolCalls.FirstOrDefault(c => c.Name == GenerateImagesToolName)`, `ReadPrompt`, `StartRunReplyAsync`). Тесты — `ImageGen/ImageGen.Core.Tests/Services/AssistantServiceTests.cs`.
- Настройки: `SessionSettings`, сериализация в `SessionRepository` (`settings_json`), `SessionService.UpdateSettingsAsync`.
- `CostEstimator` — `ImageGen/ImageGen.Core/Services/CostEstimator.cs`.

## План
1. Падающие тесты:
   - `SessionServiceTests`: обновление видео-настроек; неизвестная модель и неподдерживаемая длительность → исключение; старый JSON без полей → значения по умолчанию (Data `SessionRepositoryTests`).
   - `AssistantServiceTests`: без видео-моделей в запросе один инструмент; с ними — два; ответ с `generate_video` → вызван `StartVideoRunAsync` с моделями и параметрами сессии; `first_frame_image_id` неверного формата → `InvalidToolCall`; оба инструмента → выполнен первый, Warning.
   - `CostEstimatorTests`: Veo 8 с 1080p со звуком → цена; Seedance → в `UnknownModels`.
2. Реализация.

Файлы: `ImageGen/ImageGen.Core/Domain/Sessions/SessionSettings.cs`, `ImageGen/ImageGen.Core/Services/SessionService.cs`, `ImageGen/ImageGen.Core/Services/AssistantService.cs`, `ImageGen/ImageGen.Core/Services/AssistantPrompts.cs`, `ImageGen/ImageGen.Core/Services/CostEstimator.cs`, при необходимости `ImageGen/ImageGen.Data/Repositories/SessionRepository.cs`, тесты `ImageGen/ImageGen.Core.Tests/Services/` и `ImageGen/ImageGen.Data.Tests/Repositories/SessionRepositoryTests.cs`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
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
