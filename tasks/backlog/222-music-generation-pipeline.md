---
id: 222
status: approved
assignee:
claimed:
created: 2026-11-07
---

# Генерация аудио: запуск, сохранение, настройки сессии, инструмент ассистента

## Описание
ADR 0027; заменяет второй этап 167. Аудио — отдельный вид прогона (`MediaKind.Audio`), независимый от видео: пользователь просит в чате музыку, песню, звук или атмосферу — ассистент вызывает `generate_audio`, прогон идёт по аудио-моделям сессии, файл сохраняется сразу.

Зависит от: 221.
Среда: Linux или Windows.

## Критерии готовности
- `SessionSettings.AudioModels` (`List<ModelRef>`, пусто по умолчанию) и `SessionSettings.AudioParameters` (`Voice` — `string?`, по умолчанию `null`) в `settings_json`; `SessionService.UpdateAudioSettingsAsync(sessionId, models, parameters, ct)` с проверкой по `AudioModelCatalogService` (неизвестная → `ArgumentException`).
- `GenerationService.StartAudioRunAsync(sessionId, prompt, models, ct)` → `Run`: по генерации `Kind = Audio` на модель, сид не применяется (`SeedApplied = false`). Выполнение — синхронный поток под слотом провайдера (как картинки без очереди): `GenerateAudioAsync` → `SaveMediaStreamAsync(..., response.MediaType, ProviderResponseLimits.Audio)` → `ImagePath`, `MediaType`, `CostUsd = response.CostUsd`, `CompletedAt`, `Succeeded`; ошибки → `Failed` с видом и текстом; сохранение не удалось → `Failed`, временного файла нет. `DurationSeconds` не заполняется (длительность из файла не читается в этой задаче).
- `RecoverInterruptedAsync`: незавершённые аудио-генерации (`Queued`/`Running`, без задания) → `Failed` с `ErrorKind.Interrupted`, как картинки без задания.
- Отмена: `CancelGenerationAsync`/`CancelRunAsync` для аудио отменяют токен запроса и ставят `Cancelled`; плата провайдером за прерванный поток неизвестна — `GenerationCancelResult.StoppedWaiting` (209), чтобы UI показал предупреждение.
- `RepeatAsync`, `EditAsync` для аудио — новый аудио-прогон с той же моделью; `NewSeedAsync` и `RunOnModelAsync` — `InvalidOperationException` («not supported for audio») до записей.
- Инструмент `generate_audio` (`AssistantPrompts.GenerateAudioToolName`, описание: музыка, песни, мелодии, звуки, атмосфера, озвучка текста — по просьбе пользователя; схема — `prompt` (обязательный: описание звука/музыки) и `speech_text` (необязательный: дословный текст для речевых моделей; если не задан — используется `prompt`)) добавляется, только если у сессии есть аудио-модели. Обработка — как `generate_video` (203): первый инструмент в ответе выполняется, остальные игнорируются с Warning.
- Расход — `ExpenseKind.Music` (199 уже сопоставляет `Audio` → `Music`).
- Оценка «Next run»: речевые модели — `PricePerInputCharacterUsd × длина текста` (для оценки до отправки сообщения длина неизвестна — показывать цену за 1000 символов в описании модели, а в оценке прогона, запущенного действием карточки Repeat/Edit, — точно по тексту); модели `ChatAudio` — в `UnknownModels`.
- Тесты: прогон из двух моделей; успех сохраняет файл с типом из ответа и ценой; ошибка провайдера → `Failed`; восстановление → `Failed`; отмена → `Cancelled` + `StoppedWaiting`; Repeat/Edit → новый прогон; New seed → исключение; ассистент: инструмент есть только с аудио-моделями, вызов запускает аудио-прогон.

## Анализ
Образцы: видео-прогон 202/209 (`StartVideoRunAsync`, отмена `StoppedWaiting`), синхронные картинки (`Launch`, `RunWithProviderSlotAsync`, `InMemoryQueuedImageConnection`), настройки/инструмент 203 (`SessionSettings.VideoModels`, `GenerateVideoToolName`, разбор вызова в `AssistantService`), `CostEstimator`.

## План
1. Падающие тесты из «Критериев» (`GenerationServiceAudioTests.cs`, `AssistantServiceTests`, `SessionServiceTests`, `CostEstimatorTests`).
2. Настройки сессии и проверка моделей.
3. `StartAudioRunAsync`, выполнение, восстановление, отмена, действия.
4. Инструмент ассистента и оценка цены.

Файлы: `ImageGen/ImageGen.Core/Domain/Sessions/SessionSettings.cs`, `ImageGen/ImageGen.Core/Services/SessionService.cs`, `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core/Services/AssistantService.cs`, `ImageGen/ImageGen.Core/Services/AssistantPrompts.cs`, `ImageGen/ImageGen.Core/Services/CostEstimator.cs`, тесты `ImageGen/ImageGen.Core.Tests/Services/`, при необходимости `ImageGen/ImageGen.Data.Tests/Repositories/SessionRepositoryTests.cs`.

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

### 2026-11-07, claude-audit
Создано по просьбе владельца 2026-11-07 («генерация аудио нужна и отдельно от видео»); заменяет 167. Провайдер — OpenRouter (ADR 0027). Живой запуск LiveCheck `--only music` (208) владельцем ещё не сделан; клиент рассчитан на оба формата. План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
План одобрен ранее данным поручением «заапрувь все таски которые будут».

### 2026-11-07, claude-audit
Добавлены речевые модели (`speech`): текст для озвучки `speech_text`, голос в настройках сессии, цена по символам.
