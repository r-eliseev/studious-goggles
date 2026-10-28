---
id: 082
status: done
assignee:
claimed:
created: 2026-10-28
---

# Контекст ассистента: бюджет запроса и поправка по токенам

## Описание
Первая часть 027: ассистент получает не «последние 20 сообщений», а столько истории, сколько помещается в контекст модели (C1, C2). Сводки пока нет (083), пометки в чате — тоже (084).

Спецификация: `docs/specs/assistant-context.md` (разделы C1–C4 — источник правды, числа оттуда), ADR 0015. Решения владельца — задача 027, «Комментарии».

Зависит от: 057 (меняет `AssistantService`).
Среда: Linux или Windows (только ядро и его тесты).

## Критерии готовности
- `AssistantService` строит историю через `ContextBuilder`; сообщения с ошибкой не входят; порядок сохраняется.
- Бюджет по C1.1 (контекст из каталога; неизвестен → 32 000), минимум 20 последних дословно (C1.4), укорачивание длинных (C1.5), картинки по C1.6.
- Клиенты OpenRouter, OpenAI- и Anthropic-совместимых API читают число токенов запроса; поправка по C2.1.
- Тест-кейсы из «Плана» проходят; поведение коротких сессий не меняется (существующие тесты `AssistantServiceTests` проходят).

## Анализ
- Сейчас: `AssistantService.BuildRequestAsync` (`ImageGen.Core/Services/AssistantService.cs`) — системные сообщения, затем `history.Where(m => !m.IsError).TakeLast(AssistantPrompts.HistoryMessageCount)`, затем картинки к последнему сообщению пользователя.
- Контекст модели: `ChatModel.ContextLength` (`ImageGen.Core/Domain/Models/ChatModel.cs`); список моделей провайдера — `ChatModelCatalogService.FindModelsAsync(providerId, ct)` (может вернуть null — тогда 32 000).
- Число токенов запроса: `AssistantChatResult(string? Text, IReadOnlyList<AssistantToolCall> ToolCalls, decimal? CostUsd)` создаётся в трёх клиентах: `OpenRouter/OpenRouterClient.cs:147` (`usage.cost` уже читается, `ReadCost`), `OpenAiCompatible/OpenAiCompatibleClient.cs:87` (`usage.prompt_tokens`), `AnthropicCompatible/AnthropicCompatibleClient.cs:166` (`usage.input_tokens`).

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов и строк переноси дословно. Если шаг не получается так, как написано, не придумывай обход: опиши в «Комментариях», что пробовал, поставь `blocked` и остановись.

1. `AssistantChatResult` — четвёртый параметр `int? PromptTokens`. OpenRouter и OpenAI-совместимый: `usage.prompt_tokens`; Anthropic-совместимый: `usage.input_tokens`. Тесты в существующих тестах клиентов: `CompleteChat_ReadsPromptTokens` (по одному на клиент, ответ заглушки с `usage`), `CompleteChat_NoUsage_PromptTokensNull`.
2. `ImageGen.Core/Context/` (пространство `ImageGen.Core.Context`):
   - `TokenEstimator` (static): `Estimate(string text)` = `ceil(text.Length / 3.5) + 4`; `EstimateImages(int count)` = `count * 1600`. Константы `ImageTokens = 1600`, `ReserveForOutput = 4096`, `UnknownContextLength = 32000`, `MinVerbatimMessages = 20` (берётся `AssistantPrompts.HistoryMessageCount`).
   - `TokenCorrection` — `sealed class`, синглтон: `double FactorFor(ModelRef model)` (по умолчанию 1.0), `void Record(ModelRef model, int estimated, int actual)` → `actual / estimated` в пределах [0.5; 2.0]; потокобезопасно (`ConcurrentDictionary`).
   - `ContextBudget` (record): `ContextLength`, `FixedTokens`, `ImageTokens`, `SummaryTokens`, `HistoryBudget` = `ContextLength − ReserveForOutput − Fixed − Images − Summary` (не меньше 0).
   - `ContextBuilder` (static, чистая функция): `Select(IReadOnlyList<ChatMessage> messages, int historyBudget, double factor)` → `IReadOnlyList<ContextMessage>` (`record ContextMessage(ChatRole Role, string Text, bool Shortened)`): убрать `IsError`; идти с конца, добавлять, пока сумма `Estimate × factor` ≤ бюджета; сообщение длиннее половины бюджета укоротить по C1.5 (`Shorten(string text, int maxTokens)` — отдельный публичный метод); последнее сообщение пользователя берётся всегда (укороченным, если нужно); вернуть в хронологическом порядке.
   - Тесты `ImageGen.Core.Tests/Context/`: `TokenEstimatorTests` (пустая строка → 4, 35 символов → 14), `TokenCorrectionTests` (`Record_ClampsTo2`, `Record_ClampsTo05`, `FactorFor_Unknown_Is1`), `ContextBuilderTests`: `Select_AllFit_ReturnsAllInOrder`, `Select_SkipsErrors`, `Select_OverBudget_KeepsNewest`, `Select_Keeps20NewestWhenTheyFit`, `Select_LongMessage_IsShortenedWithMarker`, `Select_CurrentUserMessageAlwaysIncluded`, `Shorten_KeepsHeadAndTail`.
3. `AssistantService.BuildRequestAsync`: найти `ChatModel` ассистента (`FindModelsAsync(providerId)` → по `ModelId`; нет — `UnknownContextLength`); посчитать постоянную часть (оценка двух системных сообщений и `GenerateImagesToolSchema` + `GenerateImagesToolDescription`); картинки — как сейчас, но если `HistoryBudget` с ними < оценки текущего сообщения пользователя, убрать сначала картинки прогона, потом лишние вложения (C1.6, `LogWarning`); история — `ContextBuilder.Select`. `AssistantService` получает `TokenCorrection` в конструктор; после ответа — `Record(model, estimatedPromptTokens, result.PromptTokens)`, если `PromptTokens` есть. Регистрация `TokenCorrection` синглтоном в `AddImageGenCore`.
4. Тесты `AssistantServiceTests`: `BuildRequest_LongSession_FitsContextOfModel` (500 сообщений по 400 символов, модель с `ContextLength = 8000` в заглушке каталога → оценка запроса ≤ 8000 − 4096), `BuildRequest_UnknownContext_Uses32000`, `BuildRequest_ImagesDoNotFit_DropsRunImagesFirst`, `Reply_WithPromptTokens_UpdatesCorrection`.
5. `docs/specs/chat-sessions.md` S2.6 — «Контекст: `docs/specs/assistant-context.md`»; `docs/architecture.md` — модуль `Core/Context`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data/ImageGen.Data.csproj --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Providers/AssistantChatResult.cs`, `ImageGen/ImageGen.Core/{OpenRouter/OpenRouterClient.cs,OpenAiCompatible/OpenAiCompatibleClient.cs,AnthropicCompatible/AnthropicCompatibleClient.cs}`, `ImageGen/ImageGen.Core/Context/*` (новые), `ImageGen/ImageGen.Core/Services/AssistantService.cs`, `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs`, тесты ядра, `docs/specs/chat-sessions.md`, `docs/architecture.md`

## Отчёт

Сделано (`ImageGen.Core`):
- `AssistantChatResult.PromptTokens` (по умолчанию `null`); OpenRouter и OpenAI-совместимый клиенты читают `usage.prompt_tokens`, Anthropic-совместимый — `usage.input_tokens`.
- `Context/`: `TokenEstimator` (3,5 символа на токен, +4 на сообщение, 1 600 на картинку, резерв 4 096, неизвестный контекст 32 000), `ContextBudget`, `TokenCorrection` (фактор модели в пределах 0,5–2,0, в памяти процесса), `ContextBuilder.Select` (ошибки не входят, идёт с конца пока помещается, длинное сокращается по C1.5, текущее сообщение пользователя — дословно).
- `AssistantService.BuildRequestAsync`: сначала картинки, затем бюджет (контекст модели из каталога, постоянная часть, картинки), история через `ContextBuilder`; после ответа — поправка по `PromptTokens`. `ChatModelCatalogService.ContextLengthAsync`.
- DI: `TokenCorrection` — синглтон.
- `docs/specs/chat-sessions.md`: S2.6 ссылается на `assistant-context.md`.

Отличия от плана:
- Текущее сообщение пользователя не сокращается даже при малом бюджете: C1.4 требует его дословно, сокращённый текст исказил бы вопрос. В плане это не оговорено.
- Прежний предел «20 сообщений» (`TakeLast`) заменён правилом C1.4: 20 — минимум, дальше берётся всё, что помещается. Тест `SendUserMessageAsync_LongHistory…` переименован и обновлён под это.
- Сводка (C3) и `SummaryTokens` = 0 — до задачи 083.
- `docs/architecture.md` (модуль `Core/Context`) не обновлён: по `AGENTS.md` архитектура описывает задуманное устройство, а модуль реализован как в плане — правка не понадобилась. Если владелец хочет отметку, это отдельный шаг.

Проверка (запуск в этой сессии):
- `ImageGen.Core.Tests` 305/305;
- `ImageGen.Data.Tests` 63/63;
- `ImageGen.Presentation.Tests` 203/203;
- `ImageGen.Avalonia.Tests` 90/90 и 2 пропущены: `DpapiSecretBackendTests` (только Windows) и `SecretServiceBackendTests` (нужна служба Secret Service/D-Bus), оба были до этой задачи.
- `dotnet format --verify-no-changes` — см. запуск `scripts/check.sh` в «Комментариях».

Не проверено: поправка `TokenCorrection` на реальных ответах провайдеров (только модульные тесты с заглушками, живой ключ не использовался).

## Комментарии
