---
id: 010
status: planned
assignee:
claimed:
created: 2026-10-27
---

# Ядро: ассистент сессии

## Описание
`AssistantService.SendUserMessageAsync`: сохраняет сообщение пользователя, задаёт название новой сессии, отправляет модели ассистента системный промпт, текущий промпт сессии, последние 20 сообщений и инструмент `generate_images`. Вызов инструмента сразу запускает прогон и сохраняет сообщение ассистента со ссылкой на прогон и промптом. Текст без вызова — обычный ответ или вопрос. Ошибки сохраняются как сообщение ассистента с `IsError`.

Зависит от: 008, 009.
Среда: Linux или Windows (нужен только .NET 10 SDK, MAUI-нагрузки не нужны).

## Критерии готовности
- Файлы из плана созданы с содержимым как в плане; системный промпт перенесён дословно.
- Все тесты `ImageGen.Core.Tests` проходят.
- `dotnet format` для `ImageGen.Core` и `ImageGen.Core.Tests` проходит.

## Анализ
- Спецификация: S2.1–S2.6 (`docs/specs/chat-sessions.md`), ADR 0004.
- Модель ассистента по умолчанию `google/gemma-4-31b-it:free`: бесплатная, поддерживает инструменты и картинки на входе (каталог `GET /api/v1/models` на 2026-10-27). У бесплатных моделей жёсткие лимиты частоты: 429 показывается в чате как ошибка ассистента.
- Сообщения-ошибки в историю для модели не попадают.
- Промпт прогона всегда виден: сообщение ассистента заканчивается строкой `Prompt: <промпт>` (F9.4: скрытой подмены нет).
- Тексты ошибок ассистента формирует ядро на английском — упрощение скелета.
- Код проверен сборкой и тестами 2026-10-27 в пробной копии решения. Живой запрос к модели ассистента не выполнялся: ключа при подготовке не было (ручная проверка — задача 016).

## План
1. Создать файлы (содержимое дословно):

   `ImageGen/ImageGen.Core/Services/AssistantPrompts.cs`
   ```csharp
   namespace ImageGen.Core.Services;

   public static class AssistantPrompts
   {
       public const int HistoryMessageCount = 20;

       public const string GenerateImagesToolName = "generate_images";

       public const string GenerateImagesToolDescription =
           "Generate images from a complete, self-contained prompt in English.";

       public const string GenerateImagesToolSchema = """
           {"type":"object","properties":{"prompt":{"type":"string","description":"Complete image prompt in English."}},"required":["prompt"]}
           """;

       public const string System = """
           You are an assistant inside an image generation app. The user describes the image they want.
           Your job is to turn the conversation into one complete, detailed image prompt in English and call the generate_images tool with it.
           Rules:
           - If the request is clear enough, call generate_images right away. Do not ask for confirmation.
           - If something essential is missing or ambiguous, ask one short clarifying question instead of calling the tool.
           - When the user asks for changes, start from the current prompt, apply only the requested changes, and call generate_images with the full updated prompt.
           - The prompt must be self-contained: never refer to previous images or messages inside the prompt.
           - If the user only wants to talk or asks a question, answer briefly without calling the tool.
           - Reply in the user's language. The prompt is always in English.
           """;

       public const string NoCurrentPrompt = "Current prompt: (none yet)";

       public static string CurrentPrompt(string prompt) => "Current prompt: " + prompt;

       /// <summary>Text stored for an assistant message that started a run.</summary>
       public static string RunMessage(string? assistantText, string prompt) =>
           string.IsNullOrWhiteSpace(assistantText)
               ? "Prompt: " + prompt
               : assistantText.Trim() + "\n\nPrompt: " + prompt;
   }
   ```

   `ImageGen/ImageGen.Core/Services/AssistantTurnResult.cs`
   ```csharp
   using ImageGen.Core.Domain;

   namespace ImageGen.Core.Services;

   /// <param name="Run">Run started by the assistant, or null.</param>
   public sealed record AssistantTurnResult(ChatMessage UserMessage, ChatMessage AssistantMessage, Run? Run);
   ```

   `ImageGen/ImageGen.Core/Services/AssistantService.cs`
   ```csharp
   using System.Text.Json;
   using ImageGen.Core.Domain;
   using ImageGen.Core.OpenRouter;
   using ImageGen.Core.Providers;
   using ImageGen.Core.Storage;
   using Microsoft.Extensions.Logging;

   namespace ImageGen.Core.Services;

   /// <summary>Chat turn: saves the user message, asks the assistant model, starts a run when it calls generate_images.</summary>
   public sealed class AssistantService(
       SessionService sessionService,
       IChatMessageRepository messages,
       IRunRepository runs,
       IOpenRouterClient client,
       GenerationService generationService,
       TimeProvider timeProvider,
       ILogger<AssistantService> logger)
   {
       public async Task<AssistantTurnResult> SendUserMessageAsync(Guid sessionId, string text, CancellationToken cancellationToken)
       {
           ArgumentException.ThrowIfNullOrWhiteSpace(text);
           var session = await sessionService.GetAsync(sessionId, cancellationToken)
               ?? throw new InvalidOperationException($"Session {sessionId} not found.");

           var userMessage = new ChatMessage
           {
               Id = Guid.NewGuid(),
               SessionId = sessionId,
               Role = ChatRole.User,
               Text = text.Trim(),
               CreatedAt = timeProvider.GetUtcNow(),
           };
           await messages.AddAsync(userMessage, cancellationToken);
           await sessionService.SetTitleFromFirstMessageAsync(sessionId, userMessage.Text, cancellationToken);

           ChatMessage assistantMessage;
           Run? run = null;
           try
           {
               var request = await BuildRequestAsync(session, cancellationToken);
               var reply = await client.CompleteChatAsync(request, cancellationToken);
               var toolCall = reply.ToolCalls.FirstOrDefault(c => c.Name == AssistantPrompts.GenerateImagesToolName);
               if (toolCall is not null)
               {
                   var prompt = ReadPrompt(toolCall.ArgumentsJson);
                   if (prompt is null)
                   {
                       assistantMessage = CreateAssistantMessage(sessionId, "The assistant returned an invalid tool call.", isError: true);
                   }
                   else
                   {
                       try
                       {
                           run = await generationService.StartRunAsync(sessionId, prompt, cancellationToken);
                           assistantMessage = CreateAssistantMessage(sessionId, AssistantPrompts.RunMessage(reply.Text, prompt), isError: false);
                           assistantMessage.RunId = run.Id;
                       }
                       catch (InvalidOperationException ex)
                       {
                           assistantMessage = CreateAssistantMessage(sessionId, ex.Message, isError: true);
                       }
                   }
               }
               else if (!string.IsNullOrWhiteSpace(reply.Text))
               {
                   assistantMessage = CreateAssistantMessage(sessionId, reply.Text.Trim(), isError: false);
               }
               else
               {
                   assistantMessage = CreateAssistantMessage(sessionId, "The assistant returned an empty response.", isError: true);
               }
           }
           catch (ProviderException ex)
           {
               logger.LogWarning("Assistant request failed: {Kind}", ex.Kind);
               assistantMessage = CreateAssistantMessage(sessionId, $"Assistant error ({ex.Kind}): {ex.Message}", isError: true);
           }

           await messages.AddAsync(assistantMessage, cancellationToken);
           return new AssistantTurnResult(userMessage, assistantMessage, run);
       }

       private async Task<OpenRouterChatRequest> BuildRequestAsync(Session session, CancellationToken cancellationToken)
       {
           var sessionRuns = await runs.ListRunsBySessionAsync(session.Id, cancellationToken);
           var currentPrompt = sessionRuns.Count == 0
               ? AssistantPrompts.NoCurrentPrompt
               : AssistantPrompts.CurrentPrompt(sessionRuns[^1].Prompt);

           var history = (await messages.ListBySessionAsync(session.Id, cancellationToken))
               .Where(m => !m.IsError)
               .TakeLast(AssistantPrompts.HistoryMessageCount)
               .Select(m => m.Role == ChatRole.User
                   ? OpenRouterChatMessage.User(m.Text)
                   : OpenRouterChatMessage.Assistant(m.Text));

           var chat = new List<OpenRouterChatMessage>
           {
               OpenRouterChatMessage.System(AssistantPrompts.System),
               OpenRouterChatMessage.System(currentPrompt),
           };
           chat.AddRange(history);

           return new OpenRouterChatRequest(
               session.Settings.AssistantModelId,
               chat,
               [new OpenRouterTool(
                   AssistantPrompts.GenerateImagesToolName,
                   AssistantPrompts.GenerateImagesToolDescription,
                   AssistantPrompts.GenerateImagesToolSchema)]);
       }

       private ChatMessage CreateAssistantMessage(Guid sessionId, string text, bool isError) => new()
       {
           Id = Guid.NewGuid(),
           SessionId = sessionId,
           Role = ChatRole.Assistant,
           Text = text,
           CreatedAt = timeProvider.GetUtcNow(),
           IsError = isError,
       };

       /// <summary>Reads the "prompt" string from tool arguments. Returns null when missing or empty.</summary>
       public static string? ReadPrompt(string argumentsJson)
       {
           try
           {
               using var document = JsonDocument.Parse(argumentsJson);
               if (document.RootElement.ValueKind == JsonValueKind.Object
                   && document.RootElement.TryGetProperty("prompt", out var prompt)
                   && prompt.ValueKind == JsonValueKind.String
                   && !string.IsNullOrWhiteSpace(prompt.GetString()))
               {
                   return prompt.GetString()!.Trim();
               }
           }
           catch (JsonException)
           {
           }

           return null;
       }
   }
   ```

2. Создать тесты (содержимое дословно):

   `ImageGen/ImageGen.Core.Tests/Services/AssistantServiceTests.cs`
   ```csharp
   using ImageGen.Core.Domain;
   using ImageGen.Core.OpenRouter;
   using ImageGen.Core.Providers;
   using ImageGen.Core.Services;
   using ImageGen.Core.Storage;
   using Microsoft.Extensions.Logging.Abstractions;

   namespace ImageGen.Core.Tests.Services;

   public class AssistantServiceTests
   {
       private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

       private readonly ISessionRepository _sessions = Substitute.For<ISessionRepository>();
       private readonly IChatMessageRepository _messages = Substitute.For<IChatMessageRepository>();
       private readonly IRunRepository _runs = Substitute.For<IRunRepository>();
       private readonly IOpenRouterClient _client = Substitute.For<IOpenRouterClient>();
       private readonly IImageGenerator _generator = Substitute.For<IImageGenerator>();
       private readonly List<ChatMessage> _stored = [];
       private readonly Session _session;
       private readonly AssistantService _service;
       private readonly GenerationService _generation;
       private OpenRouterChatRequest? _lastRequest;

       public AssistantServiceTests()
       {
           var time = Substitute.For<TimeProvider>();
           time.GetUtcNow().Returns(Now);
           _session = new Session
           {
               Id = Guid.NewGuid(),
               Title = Session.NewSessionTitle,
               CreatedAt = Now,
               UpdatedAt = Now,
               Settings = SessionSettings.CreateDefault(),
           };
           _sessions.GetAsync(_session.Id, Arg.Any<CancellationToken>()).Returns(_session);
           _messages.AddAsync(Arg.Do<ChatMessage>(_stored.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
           _messages.ListBySessionAsync(_session.Id, Arg.Any<CancellationToken>()).Returns(_ => _stored.ToList());
           _runs.ListRunsBySessionAsync(_session.Id, Arg.Any<CancellationToken>()).Returns([]);
           _client.CompleteChatAsync(Arg.Do<OpenRouterChatRequest>(r => _lastRequest = r), Arg.Any<CancellationToken>())
               .Returns(new OpenRouterChatResult("Hello!", [], 0m));
           _generator.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>())
               .Returns(new ImageGenerationResult([1], "image/png", null));

           var models = Substitute.For<IImageModelRepository>();
           models.ListAsync(Arg.Any<CancellationToken>()).Returns([ModelCatalogServiceTests.Model(SessionSettings.DefaultImageModelId, "Klein")]);
           var catalog = new ModelCatalogService(_client, models);
           var files = Substitute.For<IImageFileStore>();
           var sessionService = new SessionService(_sessions, _messages, _runs, files, time);
           _generation = new GenerationService(
               _sessions, _runs, _generator, files, catalog, new RandomSeedSource(), time, NullLogger<GenerationService>.Instance);
           _service = new AssistantService(
               sessionService, _messages, _runs, _client, _generation, time, NullLogger<AssistantService>.Instance);
       }

       [Fact]
       public async Task SendUserMessageAsync_TextReply_SavesUserAndAssistantMessages()
       {
           var result = await _service.SendUserMessageAsync(_session.Id, " draw a fox ", TestContext.Current.CancellationToken);

           Assert.Equal("draw a fox", result.UserMessage.Text);
           Assert.Equal(ChatRole.User, result.UserMessage.Role);
           Assert.Equal("Hello!", result.AssistantMessage.Text);
           Assert.Equal(ChatRole.Assistant, result.AssistantMessage.Role);
           Assert.False(result.AssistantMessage.IsError);
           Assert.Null(result.Run);
           Assert.Equal(2, _stored.Count);
           Assert.Equal("draw a fox", _session.Title);
       }

       [Fact]
       public async Task SendUserMessageAsync_BuildsRequestWithSystemPromptCurrentPromptHistoryAndTool()
       {
           _runs.ListRunsBySessionAsync(_session.Id, Arg.Any<CancellationToken>()).Returns(
           [
               new Run { Id = Guid.NewGuid(), SessionId = _session.Id, Prompt = "old fox", Origin = RunOrigin.Assistant, CreatedAt = Now },
           ]);

           await _service.SendUserMessageAsync(_session.Id, "make it blue", TestContext.Current.CancellationToken);

           Assert.NotNull(_lastRequest);
           Assert.Equal(SessionSettings.DefaultAssistantModelId, _lastRequest.Model);
           Assert.Equal(OpenRouterChatMessage.System(AssistantPrompts.System), _lastRequest.Messages[0] with { ImageDataUrls = [] });
           Assert.Equal("Current prompt: old fox", _lastRequest.Messages[1].Text);
           Assert.Equal("user", _lastRequest.Messages[2].Role);
           Assert.Equal("make it blue", _lastRequest.Messages[2].Text);
           var tool = Assert.Single(_lastRequest.Tools);
           Assert.Equal("generate_images", tool.Name);
       }

       [Fact]
       public async Task SendUserMessageAsync_History_SkipsErrorsAndKeepsLast20()
       {
           for (var i = 0; i < 25; i++)
           {
               _stored.Add(new ChatMessage
               {
                   Id = Guid.NewGuid(),
                   SessionId = _session.Id,
                   Role = ChatRole.User,
                   Text = $"m{i}",
                   CreatedAt = Now,
                   IsError = i == 24,
               });
           }

           await _service.SendUserMessageAsync(_session.Id, "last", TestContext.Current.CancellationToken);

           var history = _lastRequest!.Messages.Skip(2).Select(m => m.Text).ToList();
           Assert.Equal(20, history.Count);
           Assert.Equal("last", history[^1]);
           Assert.DoesNotContain("m24", history);
           Assert.Equal("m5", history[0]);
       }

       [Fact]
       public async Task SendUserMessageAsync_ToolCall_StartsRunAndLinksMessage()
       {
           _client.CompleteChatAsync(Arg.Any<OpenRouterChatRequest>(), Arg.Any<CancellationToken>())
               .Returns(new OpenRouterChatResult(null, [new OpenRouterToolCall("generate_images", """{"prompt":"a red fox"}""")], 0m));

           var result = await _service.SendUserMessageAsync(_session.Id, "draw a fox", TestContext.Current.CancellationToken);
           await _generation.WhenIdleAsync();

           Assert.NotNull(result.Run);
           Assert.Equal("a red fox", result.Run.Prompt);
           Assert.Equal(result.Run.Id, result.AssistantMessage.RunId);
           Assert.Equal("Prompt: a red fox", result.AssistantMessage.Text);
           Assert.False(result.AssistantMessage.IsError);
       }

       [Fact]
       public async Task SendUserMessageAsync_ToolCallWithoutPrompt_SavesErrorMessage()
       {
           _client.CompleteChatAsync(Arg.Any<OpenRouterChatRequest>(), Arg.Any<CancellationToken>())
               .Returns(new OpenRouterChatResult(null, [new OpenRouterToolCall("generate_images", "{}")], 0m));

           var result = await _service.SendUserMessageAsync(_session.Id, "draw", TestContext.Current.CancellationToken);

           Assert.True(result.AssistantMessage.IsError);
           Assert.Null(result.Run);
       }

       [Fact]
       public async Task SendUserMessageAsync_ProviderError_SavesErrorMessageAndKeepsUserMessage()
       {
           _client.CompleteChatAsync(Arg.Any<OpenRouterChatRequest>(), Arg.Any<CancellationToken>())
               .Returns(Task.FromException<OpenRouterChatResult>(new ProviderException(ErrorKind.RateLimited, "slow down", 429)));

           var result = await _service.SendUserMessageAsync(_session.Id, "draw", TestContext.Current.CancellationToken);

           Assert.True(result.AssistantMessage.IsError);
           Assert.Equal("Assistant error (RateLimited): slow down", result.AssistantMessage.Text);
           Assert.Equal(2, _stored.Count);
       }

       [Theory]
       [InlineData("""{"prompt":" a fox "}""", "a fox")]
       [InlineData("""{"prompt":""}""", null)]
       [InlineData("""{"other":"x"}""", null)]
       [InlineData("not json", null)]
       public void ReadPrompt_Arguments_ReturnsExpected(string json, string? expected)
       {
           Assert.Equal(expected, AssistantService.ReadPrompt(json));
       }
   }
   ```

Файлы: `ImageGen/ImageGen.Core/Services/AssistantPrompts.cs`, `ImageGen/ImageGen.Core/Services/AssistantTurnResult.cs`, `ImageGen/ImageGen.Core/Services/AssistantService.cs`, `ImageGen/ImageGen.Core.Tests/Services/AssistantServiceTests.cs`

### Проверка
```
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
scripts/check.sh
```

## Отчёт

## Комментарии
