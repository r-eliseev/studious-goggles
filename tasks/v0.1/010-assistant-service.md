---
id: 010
status: done
assignee:
claimed:
created: 2026-10-27
---

# Ядро: ассистент сессии

## Описание
`AssistantService.SendUserMessageAsync`: сохраняет сообщение пользователя, задаёт название новой сессии, отправляет модели ассистента системный промпт, текущий промпт сессии, последние 20 сообщений и инструмент `generate_images`. Вызов инструмента сразу запускает прогон и сохраняет сообщение ассистента со ссылкой на прогон и промптом. Текст без вызова — обычный ответ или вопрос. Ошибки сохраняются как сообщение ассистента с `IsError`.

Чат с моделью идёт через новый узкий интерфейс `IOpenRouterChatClient`, чтобы `AssistantService` можно было тестировать с подменой.

Зависит от: 008, 009.
Среда: Linux или Windows (нужен только .NET 10 SDK, MAUI-нагрузки не нужны).

## Критерии готовности
- `IOpenRouterChatClient`, `AssistantPrompts`, `AssistantTurnResult`, `AssistantService` реализованы с поведением из плана; системный промпт и описание инструмента перенесены дословно.
- Тест-кейсы из плана написаны и проходят вместе с остальными тестами `ImageGen.Core.Tests`.
- `dotnet format` для `ImageGen.Core` и `ImageGen.Core.Tests` проходит.

## Анализ
- Спецификация: S2.1–S2.6 (`docs/specs/chat-sessions.md`), ADR 0004.
- `OpenRouterClient` — `sealed`, а `CompleteChatAsync` есть только у него: `IModelProvider` чат не описывает. Типы запроса чата (`OpenRouterChatRequest` и др.) — OpenRouter-специфичные, поэтому чат не добавляется в `IModelProvider`, а получает отдельный интерфейс в `OpenRouter/` (исключение скелета из «Правил слоёв» `docs/architecture.md`: ассистент работает только через OpenRouter).
- Модель ассистента по умолчанию `google/gemma-4-31b-it:free`: бесплатная, поддерживает инструменты и картинки на входе (каталог `GET /api/v1/models` на 2026-10-27). У бесплатных моделей жёсткие лимиты частоты: 429 показывается в чате как ошибка ассистента.
- Сообщения-ошибки в историю для модели не попадают.
- Промпт прогона всегда виден: сообщение ассистента заканчивается строкой `Prompt: <промпт>` (F9.4: скрытой подмены нет).
- Тексты ошибок ассистента формирует ядро на английском — упрощение скелета.
- Пробная реализация: `poc/v0.1/010-assistant-service/` (подсказка, не копировать дословно, см. `poc/README.md`). Живой запрос к модели ассистента не выполнялся (ручная проверка — задача 016).

## План
1. `OpenRouter/IOpenRouterChatClient.cs`: один метод `CompleteChatAsync(OpenRouterChatRequest, ct) → OpenRouterChatResult`, бросает `ProviderException`. `OpenRouterClient` объявляет, что реализует его (метод уже есть, менять тело не нужно).
2. `Services/AssistantPrompts.cs`, статический класс с константами:
   - `HistoryMessageCount = 20`, `GenerateImagesToolName = "generate_images"`.
   - `GenerateImagesToolDescription`: `Generate images from a complete, self-contained prompt in English.`
   - `GenerateImagesToolSchema` (JSON Schema строкой): объект с обязательным строковым полем `prompt`, описание поля `Complete image prompt in English.`
   - `System` — дословно:
     ```text
     You are an assistant inside an image generation app. The user describes the image they want.
     Your job is to turn the conversation into one complete, detailed image prompt in English and call the generate_images tool with it.
     Rules:
     - If the request is clear enough, call generate_images right away. Do not ask for confirmation.
     - If something essential is missing or ambiguous, ask one short clarifying question instead of calling the tool.
     - When the user asks for changes, start from the current prompt, apply only the requested changes, and call generate_images with the full updated prompt.
     - The prompt must be self-contained: never refer to previous images or messages inside the prompt.
     - If the user only wants to talk or asks a question, answer briefly without calling the tool.
     - Reply in the user's language. The prompt is always in English.
     ```
   - `NoCurrentPrompt = "Current prompt: (none yet)"`, `CurrentPrompt(p) = "Current prompt: " + p`.
   - `RunMessage(assistantText, prompt)`: без текста → `Prompt: <prompt>`; с текстом → `<текст>` + пустая строка + `Prompt: <prompt>`.
3. `Services/AssistantTurnResult.cs`: запись `(ChatMessage UserMessage, ChatMessage AssistantMessage, Run? Run)`.
4. `Services/AssistantService.cs`, `sealed`. Зависимости: `SessionService`, `IChatMessageRepository`, `IRunRepository`, `IOpenRouterChatClient`, `GenerationService`, `TimeProvider`, `ILogger<AssistantService>`.
   - `SendUserMessageAsync(sessionId, text, ct)`:
     ```
     пустой text → ArgumentException; сессии нет → InvalidOperationException
     сохранить сообщение User (text.Trim())
     sessionService.SetTitleFromFirstMessageAsync(...)
     try:
         request = BuildRequest(session)
         reply = client.CompleteChatAsync(request)
         если есть вызов generate_images:
             prompt = ReadPrompt(arguments)
             prompt нет → ошибка "The assistant returned an invalid tool call."
             иначе run = generationService.StartRunAsync(session, prompt)
                  → ответ RunMessage(reply.Text, prompt), RunId = run.Id
                  (InvalidOperationException, например нет моделей → ошибка с её текстом)
         иначе если есть текст → обычный ответ (Trim)
         иначе → ошибка "The assistant returned an empty response."
     catch ProviderException → ошибка "Assistant error (<Kind>): <message>", в лог только Kind
     сохранить сообщение Assistant (IsError для ошибок)
     вернуть AssistantTurnResult
     ```
   - `BuildRequest(session)`: модель — `Settings.AssistantModelId`; сообщения: `System`, затем системное с текущим промптом (промпт последнего прогона сессии или `NoCurrentPrompt`), затем последние 20 сообщений сессии без `IsError` в хронологическом порядке (роль User → user, остальное → assistant); один инструмент `generate_images`.
   - `static ReadPrompt(argumentsJson)`: строковое непустое поле `prompt` объекта (обрезанное); иначе, в том числе при битом JSON, `null`.
5. Тесты `ImageGen.Core.Tests/Services/AssistantServiceTests.cs`: настоящие `SessionService` и `GenerationService` над подменёнными репозиториями, подменённые `IOpenRouterChatClient` и `IImageGenerator`; запрос к клиенту перехватывается для проверок.
   - Текстовый ответ → сохранены сообщение User и Assistant без ошибки, прогона нет.
   - Запрос: модель ассистента из настроек; первые два сообщения — системный промпт и `Current prompt: …`; последний инструмент — `generate_images`.
   - История: 25 сообщений, из них часть с `IsError` → в запрос попадают только последние 20 без ошибок.
   - Вызов инструмента с промптом → прогон запущен, сообщение ассистента ссылается на прогон и заканчивается `Prompt: <промпт>`.
   - Вызов инструмента без `prompt` → сообщение-ошибка, прогона нет.
   - `ProviderException(RateLimited)` → сообщение User сохранено, сообщение Assistant с `IsError`.
   - `ReadPrompt`: валидный объект → промпт; пустая строка, не строка, нет поля, битый JSON → `null`.

Файлы: `ImageGen/ImageGen.Core/OpenRouter/IOpenRouterChatClient.cs`, `ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs`, `ImageGen/ImageGen.Core/Services/AssistantPrompts.cs`, `ImageGen/ImageGen.Core/Services/AssistantTurnResult.cs`, `ImageGen/ImageGen.Core/Services/AssistantService.cs`, `ImageGen/ImageGen.Core.Tests/Services/AssistantServiceTests.cs`

### Проверка
```
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
scripts/check.sh
```

## Отчёт
- Добавлены `OpenRouter/IOpenRouterChatClient.cs` (один метод `CompleteChatAsync`); `OpenRouterClient` теперь объявляет `IOpenRouterChatClient`, тело метода не менялось.
- Добавлены `Services/AssistantPrompts.cs` (системный промпт и описание инструмента дословно из плана, `RunMessage`, `CurrentPrompt`), `Services/AssistantTurnResult.cs`, `Services/AssistantService.cs` (`SendUserMessageAsync`, внутренний `ReadPrompt`).
- Тесты: `ImageGen.Core.Tests/Services/AssistantServiceTests.cs`, 21 тест (текст, запрос, история 20 без ошибок, прогон, вызов без промпта, пустой ответ, нет моделей, `RateLimited`, пустой текст, неизвестная сессия, `ReadPrompt`). Вся `ImageGen.Core.Tests` проходит: 94 теста, 0 падений.
- `dotnet format --verify-no-changes` для `ImageGen.Core` и `ImageGen.Core.Tests` проходит без изменений. `scripts/check.sh` проходит.
- Отклонение от плана: `GenerationService.StartRunAsync` в репозитории принимает `(sessionId, prompt, ct)`, а не `(session, prompt)`; вызывается с `sessionId`.
- Отклонение от плана: `ReadPrompt` объявлен `internal static` (тесты видят его через `InternalsVisibleTo`), а не `public`.
- Отклонение от плана: разделитель между текстом ассистента и строкой `Prompt:` равен `\n\n`, а не `Environment.NewLine`, чтобы текст не зависел от ОС.
- Отклонение от плана: имена тестов в английском стиле `Метод_Условие_Результат` (как в существующих тестах ядра), описания тест-кейсов из плана сохранены по смыслу.
- Не сделано (вне плана): стоимость чата `CostUsd` из ответа ассистента не сохраняется, в `ChatMessage` нет поля для неё.
- Не сделано (вне плана): `OperationCanceledException` при отмене во время запроса к ассистенту пробрасывается наружу, сообщение ассистента не сохраняется (сообщение пользователя уже записано).
- Живой запрос к модели ассистента не выполнялся (тесты без сети; ручная проверка — задача 016).
- Документация `docs/` не менялась (план её не требовал; раздел «Состояние кода» в `docs/architecture.md` не дописывался).
- Файл задачи после `scripts/release-task.sh 010` (локально): `status: done`, поля `assignee` и `claimed` очищены этим скриптом, как и задумано.

## Комментарии
