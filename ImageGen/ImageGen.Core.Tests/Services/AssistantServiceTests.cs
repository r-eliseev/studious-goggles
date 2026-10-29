using ImageGen.Core.Context;
using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using ImageGen.Core.Tests.TestDoubles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Core.Tests.Services;

public class AssistantServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private const string ImageModel = "vendor/image";
    private const string AssistantModel = "vendor/assistant";
    private const string GenerateImages = AssistantPrompts.GenerateImagesToolName;
    private static readonly byte[] Image = [1, 2, 3];

    private readonly ISessionRepository _sessions = Substitute.For<ISessionRepository>();
    private readonly IChatMessageRepository _messages = Substitute.For<IChatMessageRepository>();
    private readonly IRunRepository _runs = Substitute.For<IRunRepository>();
    private readonly IImageFileStore _files = Substitute.For<IImageFileStore>();
    private readonly IModelProvider _generator = Substitute.For<IModelProvider>();
    private readonly IProviderRouter _providers = Substitute.For<IProviderRouter>();
    private readonly ISeedSource _seeds = Substitute.For<ISeedSource>();
    private readonly IImageModelRepository _modelRepository = Substitute.For<IImageModelRepository>();
    private readonly IAssistantChatClient _chat = Substitute.For<IAssistantChatClient>();
    private readonly List<ChatMessage> _stored = [];
    private readonly List<Run> _storedRuns = [];
    private readonly List<Generation> _storedGenerations = [];
    private readonly IChatAttachmentRepository _chatAttachments = Substitute.For<IChatAttachmentRepository>();
    private readonly Session _session;
    private readonly GenerationService _generations;
    private readonly CapturingLogger<AssistantService> _logger = new();
    private readonly IConversationSummaryRepository _summaries = Substitute.For<IConversationSummaryRepository>();
    private readonly AssistantService _service;

    public AssistantServiceTests()
    {
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(Now);

        _modelRepository.ListAsync(ProviderIds.OpenRouter, Arg.Any<CancellationToken>()).Returns(
            [ModelCatalogServiceTests.Model(ImageModel, "Image")]);
        _providers.ImageProviderIds.Returns([ProviderIds.OpenRouter]);
        _providers.GetImageProvider(ProviderIds.OpenRouter).Returns(_generator);
        _providers.GetChatClient(ProviderIds.OpenRouter).Returns(_chat);
        var catalog = new ModelCatalogService(_providers, _modelRepository, NullLogger<ModelCatalogService>.Instance);
        _seeds.NextSeed().Returns(7L);
        _generator.GenerateAsync(Arg.Any<ImageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ImageGenerationResult(Image, "image/png", 0.01m));
        _files.SaveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("images/session/generation.png");

        _session = new Session
        {
            Id = Guid.NewGuid(),
            Title = Session.NewSessionTitle,
            CreatedAt = Now,
            UpdatedAt = Now,
            Settings = new SessionSettings
            {
                ImageModels = [new ModelRef(ProviderIds.OpenRouter, ImageModel)],
                RunsPerModel = 1,
                AssistantModel = new ModelRef(ProviderIds.OpenRouter, AssistantModel),
            },
        };
        _sessions.GetAsync(_session.Id, Arg.Any<CancellationToken>()).Returns(_session);

        _messages.AddAsync(Arg.Any<ChatMessage>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                _stored.Add(ci.Arg<ChatMessage>());
                return Task.CompletedTask;
            });
        _messages.ListBySessionAsync(_session.Id, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<ChatMessage>>(_stored.ToList()));
        _runs.AddRunAsync(Arg.Any<Run>(), Arg.Any<IReadOnlyList<Generation>>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                _storedRuns.Add(ci.Arg<Run>());
                return Task.CompletedTask;
            });
        _runs.ListRunsBySessionAsync(_session.Id, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<Run>>(_storedRuns.ToList()));
        _runs.ListGenerationsBySessionAsync(_session.Id, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<Generation>>(_storedGenerations.ToList()));
        _chat.ListChatModelsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<ChatModel>>([]));
        _files.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([1, 2, 3]);

        var sessionService = new SessionService(
            _sessions, _messages, _runs, _files, new ChatModelCatalogService(_providers, NullLogger<ChatModelCatalogService>.Instance), time, Substitute.For<IConversationSummaryRepository>());
        var attachments = new AttachmentService(_chatAttachments, _files, time);
        _generations = new GenerationService(
            _sessions, _runs, _providers, _files, catalog, _seeds, attachments, time, NullLogger<GenerationService>.Instance);
        var chatModels = new ChatModelCatalogService(_providers, NullLogger<ChatModelCatalogService>.Instance);
        _summaries.GetAsync(_session.Id, Arg.Any<CancellationToken>()).Returns((ConversationSummary?)null);
        var correction = new TokenCorrection();
        // The summary job is tested on its own (ConversationSummaryServiceTests). Here it finds no session and does nothing,
        // so its calls to the model do not mix with the calls of the assistant turn.
        var summaryService = new ConversationSummaryService(
            Substitute.For<ISessionRepository>(), _messages, _runs, _summaries, _providers, chatModels, correction, time,
            NullLogger<ConversationSummaryService>.Instance);
        _service = new AssistantService(
            sessionService, _messages, _runs, _providers, _generations, chatModels, _files, attachments, time, _logger, correction,
            _summaries, summaryService);
    }

    private ChatMessage AssistantMessage => _stored.Last();

    private static readonly DateTimeOffset ImagesNow = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Stores a run with the given generations (sort order, status, image path); the last stored run is the current one.</summary>
    private void AddRunWithGenerations(params (int SortOrder, GenerationStatus Status, string? ImagePath)[] generations)
    {
        var run = new Run
        {
            Id = Guid.NewGuid(),
            SessionId = _session.Id,
            Prompt = "fox",
            Origin = RunOrigin.Assistant,
            CreatedAt = ImagesNow.AddMinutes(-5),
        };
        _storedRuns.Add(run);
        _storedGenerations.AddRange(generations.Select(g => new Generation
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            SessionId = _session.Id,
            ProviderId = ProviderIds.OpenRouter,
            ModelId = ImageModel,
            Prompt = "fox",
            Seed = 1,
            SeedApplied = true,
            Status = g.Status,
            ImagePath = g.ImagePath,
            MediaType = "image/png",
            CreatedAt = ImagesNow,
            SortOrder = g.SortOrder,
        }));
    }

    private void AssistantAcceptsImages() =>
        _chat.ListChatModelsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<ChatModel>>(
            [new ChatModel(AssistantModel, "Assistant", SupportsImageInput: true, ContextLength: 1000)]));

    private ChatAttachment UserAttachment()
    {
        var attachment = new ChatAttachment
        {
            Id = Guid.NewGuid(),
            SessionId = _session.Id,
            RelativePath = $"images/{_session.Id:N}/attachments/{Guid.NewGuid():N}.png",
            MediaType = "image/png",
            CreatedAt = ImagesNow,
        };
        _chatAttachments.GetAsync(attachment.Id, Arg.Any<CancellationToken>()).Returns(attachment);
        _files.ReadAsync(attachment.RelativePath, Arg.Any<CancellationToken>()).Returns([7, 7]);
        return attachment;
    }

    [Fact]
    public async Task SendUserMessageAsync_WithAttachment_LinksItAndSendsItToAssistantThatTakesImages()
    {
        AssistantAcceptsImages();
        var attachment = UserAttachment();
        ReplyWith(new AssistantChatResult("Hi", [], null));

        var result = await _service.SendUserMessageAsync(_session.Id, "look at this", [attachment.Id], TestContext.Current.CancellationToken);

        await _chatAttachments.Received(1).LinkToMessageAsync(
            Arg.Is<IReadOnlyList<Guid>>(ids => ids.SequenceEqual(new[] { attachment.Id })),
            result.UserMessage.Id,
            Arg.Any<CancellationToken>());
        var user = LastRequest.Messages.Last(m => m.Role == AssistantChatMessage.UserRole);
        Assert.Equal("data:image/png;base64,Bwc=", Assert.Single(user.ImageDataUrls));
    }

    [Fact]
    public async Task SendUserMessageAsync_OnlyAttachmentAndNoText_IsAccepted()
    {
        AssistantAcceptsImages();
        var attachment = UserAttachment();
        ReplyWith(new AssistantChatResult("Hi", [], null));

        var result = await _service.SendUserMessageAsync(_session.Id, " ", [attachment.Id], TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, result.UserMessage.Text);
    }

    [Fact]
    public async Task SendUserMessageAsync_NoTextAndNoAttachment_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SendUserMessageAsync(_session.Id, " ", [], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendUserMessageAsync_MoreThanFourAttachments_Throws()
    {
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SendUserMessageAsync(_session.Id, "x", ids, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendUserMessageAsync_GenerateWithAttachmentsAndNoReferenceModel_ReplyIsErrorWithReason()
    {
        var attachment = UserAttachment();
        ReplyWithToolCall("{\"prompt\":\"a fox\"}");

        var result = await _service.SendUserMessageAsync(_session.Id, "draw", [attachment.Id], TestContext.Current.CancellationToken);

        Assert.Null(result.Run);
        Assert.True(result.AssistantMessage.IsError);
        Assert.Equal(ErrorKind.ModelWithoutReferences, result.AssistantMessage.ErrorKind);
    }

    [Fact]
    public async Task SendUserMessageAsync_AssistantAcceptsImages_AttachesRunImagesToCurrentUserMessage()
    {
        AssistantAcceptsImages();
        AddRunWithGenerations((1, GenerationStatus.Succeeded, "images/b.png"), (0, GenerationStatus.Succeeded, "images/a.png"));
        ReplyWith(new AssistantChatResult("Hi", [], null));

        await _service.SendUserMessageAsync(_session.Id, "make it red", TestContext.Current.CancellationToken);

        var user = LastRequest.Messages.Last(m => m.Role == AssistantChatMessage.UserRole);
        Assert.Equal("make it red", user.Text);
        Assert.Equal(2, user.ImageDataUrls.Count);
        Assert.All(user.ImageDataUrls, url => Assert.StartsWith("data:image/png;base64,", url, StringComparison.Ordinal));
        await _files.Received(1).ReadAsync("images/a.png", Arg.Any<CancellationToken>());
        await _files.Received(1).ReadAsync("images/b.png", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendUserMessageAsync_AssistantWithoutImageInput_SendsNoImagesAndReadsNoFiles()
    {
        AddRunWithGenerations((0, GenerationStatus.Succeeded, "images/a.png"));
        ReplyWith(new AssistantChatResult("Hi", [], null));

        await _service.SendUserMessageAsync(_session.Id, "make it red", TestContext.Current.CancellationToken);

        Assert.All(LastRequest.Messages, m => Assert.Empty(m.ImageDataUrls));
        await _files.DidNotReceive().ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendUserMessageAsync_MoreThanFourSucceeded_SendsFirstFourBySortOrder()
    {
        AssistantAcceptsImages();
        AddRunWithGenerations(
            (0, GenerationStatus.Succeeded, "images/0.png"),
            (1, GenerationStatus.Succeeded, "images/1.png"),
            (2, GenerationStatus.Succeeded, "images/2.png"),
            (3, GenerationStatus.Succeeded, "images/3.png"),
            (4, GenerationStatus.Succeeded, "images/4.png"),
            (5, GenerationStatus.Succeeded, "images/5.png"));
        ReplyWith(new AssistantChatResult("Hi", [], null));

        await _service.SendUserMessageAsync(_session.Id, "more", TestContext.Current.CancellationToken);

        Assert.Equal(4, LastRequest.Messages.Last(m => m.Role == AssistantChatMessage.UserRole).ImageDataUrls.Count);
        await _files.DidNotReceive().ReadAsync("images/4.png", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendUserMessageAsync_OnlyFailedGenerations_SendsNoImages()
    {
        AssistantAcceptsImages();
        AddRunWithGenerations(
            (0, GenerationStatus.Failed, null),
            (1, GenerationStatus.Filtered, null),
            (2, GenerationStatus.Queued, null));
        ReplyWith(new AssistantChatResult("Hi", [], null));

        await _service.SendUserMessageAsync(_session.Id, "more", TestContext.Current.CancellationToken);

        Assert.All(LastRequest.Messages, m => Assert.Empty(m.ImageDataUrls));
    }

    [Fact]
    public async Task SendUserMessageAsync_NoRuns_SendsNoImages()
    {
        AssistantAcceptsImages();
        ReplyWith(new AssistantChatResult("Hi", [], null));

        await _service.SendUserMessageAsync(_session.Id, "first", TestContext.Current.CancellationToken);

        Assert.All(LastRequest.Messages, m => Assert.Empty(m.ImageDataUrls));
    }

    [Fact]
    public async Task SendUserMessageAsync_OneImageFileMissing_SendsTheOthersWithoutError()
    {
        AssistantAcceptsImages();
        AddRunWithGenerations((0, GenerationStatus.Succeeded, "images/gone.png"), (1, GenerationStatus.Succeeded, "images/kept.png"));
        _files.ReadAsync("images/gone.png", Arg.Any<CancellationToken>())
            .Returns<byte[]>(_ => throw new FileNotFoundException("gone"));
        ReplyWith(new AssistantChatResult("Hi", [], null));

        var result = await _service.SendUserMessageAsync(_session.Id, "more", TestContext.Current.CancellationToken);

        Assert.Single(LastRequest.Messages.Last(m => m.Role == AssistantChatMessage.UserRole).ImageDataUrls);
        Assert.False(result.AssistantMessage.IsError);
    }

    [Fact]
    public async Task SendUserMessageAsync_CatalogFails_SendsNoImagesAndNoError()
    {
        AddRunWithGenerations((0, GenerationStatus.Succeeded, "images/a.png"));
        _chat.ListChatModelsAsync(Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<ChatModel>>>(_ => throw new ProviderException(ErrorKind.Network, "down"));
        ReplyWith(new AssistantChatResult("Hi", [], null));

        var result = await _service.SendUserMessageAsync(_session.Id, "more", TestContext.Current.CancellationToken);

        Assert.All(LastRequest.Messages, m => Assert.Empty(m.ImageDataUrls));
        Assert.False(result.AssistantMessage.IsError);
    }

    [Fact]
    public async Task SendUserMessageAsync_Information_DoesNotLogUserText()
    {
        ReplyWith(new AssistantChatResult("Reply text", [], null));

        await _service.SendUserMessageAsync(_session.Id, "my private prompt", TestContext.Current.CancellationToken);

        Assert.DoesNotContain(
            _logger.Entries.Where(e => e.Level >= LogLevel.Information),
            e => e.Message.Contains("my private prompt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendUserMessageAsync_Debug_LogsUserTextAndReply()
    {
        ReplyWith(new AssistantChatResult("Reply text", [], null));

        await _service.SendUserMessageAsync(_session.Id, "my private prompt", TestContext.Current.CancellationToken);

        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Debug && e.Message.Contains("my private prompt", StringComparison.Ordinal));
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Debug && e.Message.Contains("Reply text", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendUserMessageAsync_ToolCall_LogsTurnWithRunAndToolCallCount()
    {
        ReplyWithToolCall("{\"prompt\":\"a fox\"}");

        var result = await _service.SendUserMessageAsync(_session.Id, "draw a fox", TestContext.Current.CancellationToken);

        var finished = Assert.Single(_logger.Entries, e => e.Message.Contains("finished", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Information, finished.Level);
        Assert.Contains($"run {result.Run!.Id}", finished.Message, StringComparison.Ordinal);
        Assert.Contains("1 tool calls", finished.Message, StringComparison.Ordinal);
    }


    private AssistantChatRequest LastRequest =>
        (AssistantChatRequest)_chat.ReceivedCalls()
            .Last(call => call.GetMethodInfo().Name == nameof(IAssistantChatClient.CompleteChatAsync))
            .GetArguments()[0]!;

    private void ReplyWith(AssistantChatResult reply) =>
        _chat.CompleteChatAsync(Arg.Any<AssistantChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(reply);

    private void ReplyWithToolCall(string argumentsJson, string? text = null) =>
        ReplyWith(new AssistantChatResult(text, [new AssistantToolCall(GenerateImages, argumentsJson)], 0.001m));

    [Fact]
    public async Task SendUserMessageAsync_TextReply_SavesUserAndAssistantWithoutRun()
    {
        ReplyWith(new AssistantChatResult("  Which style do you want?  ", [], null));

        var result = await _service.SendUserMessageAsync(_session.Id, "  a fox  ", TestContext.Current.CancellationToken);

        Assert.Null(result.Run);
        Assert.Equal(ChatRole.User, result.UserMessage.Role);
        Assert.Equal("a fox", result.UserMessage.Text);
        Assert.Equal(ChatRole.Assistant, result.AssistantMessage.Role);
        Assert.Equal("Which style do you want?", result.AssistantMessage.Text);
        Assert.False(result.AssistantMessage.IsError);
        Assert.Null(result.AssistantMessage.RunId);
        Assert.Equal(2, _stored.Count);
        Assert.Empty(_storedRuns);
    }

    [Fact]
    public async Task SendUserMessageAsync_FirstMessage_SetsSessionTitle()
    {
        ReplyWith(new AssistantChatResult("Hi", [], null));

        await _service.SendUserMessageAsync(_session.Id, "  a fox  ", TestContext.Current.CancellationToken);

        Assert.Equal("a fox", _session.Title);
    }

    [Fact]
    public async Task SendUserMessageAsync_LongSession_RequestFitsContextOfModel()
    {
        _chat.ListChatModelsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<ChatModel>>(
            [new ChatModel(AssistantModel, "Assistant", SupportsImageInput: false, ContextLength: 8000)]));
        SeedLongHistory(500);
        ReplyWith(new AssistantChatResult("ok", [], null));

        await _service.SendUserMessageAsync(_session.Id, "a fox", TestContext.Current.CancellationToken);

        var request = LastRequest;
        Assert.True(request.Messages.Count < 500);
        Assert.True(request.Messages.Sum(m => TokenEstimator.Estimate(m.Text)) <= 8000 - TokenEstimator.ReserveForOutput);
    }

    [Fact]
    public async Task SendUserMessageAsync_UnknownContext_UsesDefaultContextLength()
    {
        SeedLongHistory(500);
        ReplyWith(new AssistantChatResult("ok", [], null));

        await _service.SendUserMessageAsync(_session.Id, "a fox", TestContext.Current.CancellationToken);

        Assert.True(LastRequest.Messages.Sum(m => TokenEstimator.Estimate(m.Text)) <= TokenEstimator.UnknownContextLength - TokenEstimator.ReserveForOutput);
    }

    private void SeedLongHistory(int count)
    {
        for (var i = 0; i < count; i++)
        {
            _stored.Add(new ChatMessage
            {
                Id = Guid.NewGuid(),
                SessionId = _session.Id,
                Role = i % 2 == 0 ? ChatRole.User : ChatRole.Assistant,
                Text = new string('x', 400),
                CreatedAt = Now.AddSeconds(i),
            });
        }
    }

    [Fact]
    public async Task SendUserMessageAsync_Request_UsesAssistantModelPromptsAndTool()
    {
        ReplyWith(new AssistantChatResult("Hi", [], null));

        await _service.SendUserMessageAsync(_session.Id, "a fox", TestContext.Current.CancellationToken);

        var request = LastRequest;
        Assert.Equal(AssistantModel, request.Model);
        Assert.Equal(AssistantPrompts.System, request.Messages[0].Text);
        Assert.Equal(AssistantChatMessage.SystemRole, request.Messages[0].Role);
        Assert.Equal(AssistantPrompts.NoCurrentPrompt, request.Messages[1].Text);
        Assert.Equal(AssistantChatMessage.UserRole, request.Messages[2].Role);
        Assert.Equal("a fox", request.Messages[2].Text);
        var tool = Assert.Single(request.Tools);
        Assert.Equal(GenerateImages, tool.Name);
        Assert.Equal(AssistantPrompts.GenerateImagesToolDescription, tool.Description);
        Assert.Equal(AssistantPrompts.GenerateImagesToolSchema, tool.ParametersJsonSchema);
    }

    [Fact]
    public async Task SendUserMessageAsync_ExistingRun_SendsItsPromptAsCurrentPrompt()
    {
        _storedRuns.Add(new Run
        {
            Id = Guid.NewGuid(),
            SessionId = _session.Id,
            Prompt = "old fox",
            Origin = RunOrigin.Assistant,
            CreatedAt = Now.AddMinutes(-5),
        });
        ReplyWith(new AssistantChatResult("Hi", [], null));

        await _service.SendUserMessageAsync(_session.Id, "make it red", TestContext.Current.CancellationToken);

        Assert.Equal("Current prompt: old fox", LastRequest.Messages[1].Text);
    }

    [Fact]
    public async Task SendUserMessageAsync_ShortHistory_SendsAllNonErrorMessagesInOrder()
    {
        for (var i = 0; i < 25; i++)
        {
            _stored.Add(new ChatMessage
            {
                Id = Guid.NewGuid(),
                SessionId = _session.Id,
                Role = i % 2 == 0 ? ChatRole.User : ChatRole.Assistant,
                Text = i % 5 == 0 ? $"error {i}" : $"m{i}",
                CreatedAt = Now,
                IsError = i % 5 == 0,
            });
        }
        ReplyWith(new AssistantChatResult("Hi", [], null));

        await _service.SendUserMessageAsync(_session.Id, "new", TestContext.Current.CancellationToken);

        // C1.4: everything that fits is sent (the 20 newest are a minimum, not a maximum): all 21 non-error messages fit.
        var history = LastRequest.Messages.Skip(2).ToList();
        Assert.Equal(21, history.Count);
        Assert.Equal("m1", history[0].Text);
        Assert.Equal("new", history[^1].Text);
        Assert.DoesNotContain(history, m => m.Text.StartsWith("error", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendUserMessageAsync_SummaryExists_SendsSummaryBeforeHistoryAndSkipsCoveredMessages()
    {
        for (var i = 0; i < 6; i++)
        {
            _stored.Add(new ChatMessage
            {
                Id = Guid.NewGuid(),
                SessionId = _session.Id,
                Role = i % 2 == 0 ? ChatRole.User : ChatRole.Assistant,
                Text = $"m{i}",
                CreatedAt = Now.AddSeconds(i),
            });
        }
        _summaries.GetAsync(_session.Id, Arg.Any<CancellationToken>()).Returns(new ConversationSummary
        {
            SessionId = _session.Id,
            Text = "earlier decisions",
            CoversUpToMessageId = _stored[2].Id,
            Model = new ModelRef(ProviderIds.OpenRouter, AssistantModel),
            CreatedAt = Now,
        });
        ReplyWith(new AssistantChatResult("Hi", [], null));

        await _service.SendUserMessageAsync(_session.Id, "new", TestContext.Current.CancellationToken);

        // C1.3: system prompt, current prompt, summary, then the messages after the covered part.
        var request = LastRequest;
        Assert.Equal(AssistantPrompts.SummaryLine("earlier decisions"), request.Messages[2].Text);
        Assert.Equal(AssistantChatMessage.SystemRole, request.Messages[2].Role);
        Assert.Equal(["m3", "m4", "m5", "new"], request.Messages.Skip(3).Select(m => m.Text));
        Assert.DoesNotContain(request.Messages, m => m.Text is "m0" or "m1" or "m2");
    }

    private void StoreErrorTurn(string userText, string errorText)
    {
        _stored.Add(new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = _session.Id,
            Role = ChatRole.User,
            Text = userText,
            CreatedAt = Now,
        });
        _stored.Add(new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = _session.Id,
            Role = ChatRole.Assistant,
            Text = errorText,
            CreatedAt = Now.AddSeconds(1),
            IsError = true,
        });
    }

    [Fact]
    public async Task RetryLastTurn_SendsLastUserMessageAgain()
    {
        StoreErrorTurn("a fox", "Assistant error (RateLimited): slow down");
        ReplyWith(new AssistantChatResult("Here you go", [], null));

        await _service.RetryLastTurnAsync(_session.Id, TestContext.Current.CancellationToken);

        Assert.Equal("a fox", LastRequest.Messages[^1].Text);
        Assert.Equal(AssistantChatMessage.UserRole, LastRequest.Messages[^1].Role);
    }

    [Fact]
    public async Task RetryLastTurn_DoesNotAddUserMessage_AddsOnlyTheReply()
    {
        StoreErrorTurn("a fox", "error");
        ReplyWith(new AssistantChatResult("Here you go", [], null));

        var result = await _service.RetryLastTurnAsync(_session.Id, TestContext.Current.CancellationToken);

        Assert.Equal(3, _stored.Count);
        Assert.Equal(1, _stored.Count(m => m.Role == ChatRole.User));
        Assert.Equal("Here you go", result.AssistantMessage.Text);
        Assert.False(result.AssistantMessage.IsError);
    }

    [Fact]
    public async Task RetryLastTurn_ExcludesErrorFromHistory()
    {
        StoreErrorTurn("a fox", "Assistant error (RateLimited): slow down");
        ReplyWith(new AssistantChatResult("Here you go", [], null));

        await _service.RetryLastTurnAsync(_session.Id, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(LastRequest.Messages, m => m.Text.Contains("slow down", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RetryLastTurn_NoUserMessage_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RetryLastTurnAsync(_session.Id, TestContext.Current.CancellationToken));
        Assert.Empty(_stored);
    }

    [Fact]
    public async Task RetryLastTurn_WithAttachments_SendsImagesAgain()
    {
        AssistantAcceptsImages();
        var attachment = UserAttachment();
        StoreErrorTurn("look at this", "error");
        attachment.MessageId = _stored[0].Id;
        _chatAttachments.ListByMessageAsync(_stored[0].Id, Arg.Any<CancellationToken>()).Returns([attachment]);
        ReplyWith(new AssistantChatResult("Hi", [], null));

        await _service.RetryLastTurnAsync(_session.Id, TestContext.Current.CancellationToken);

        Assert.Single(LastRequest.Messages[^1].ImageDataUrls);
    }

    [Fact]
    public async Task SendUserMessageAsync_ToolCallWithPrompt_StartsRunAndShowsPrompt()
    {
        var ct = TestContext.Current.CancellationToken;
        ReplyWithToolCall("""{"prompt":"a red fox in snow"}""", "Here is the plan.");

        var result = await _service.SendUserMessageAsync(_session.Id, "a fox", ct);
        await _generations.WhenIdleAsync();

        Assert.NotNull(result.Run);
        Assert.Equal("a red fox in snow", result.Run.Prompt);
        Assert.Equal(RunOrigin.Assistant, result.Run.Origin);
        Assert.Equal(result.Run.Id, result.AssistantMessage.RunId);
        Assert.False(result.AssistantMessage.IsError);
        Assert.Equal("Here is the plan.", result.AssistantMessage.Text);
        await _generator.Received(1).GenerateAsync(
            Arg.Is<ImageGenerationRequest>(r => r.Prompt == "a red fox in snow" && r.ModelId == ImageModel),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendUserMessageAsync_ToolCallWithoutText_RunMessageIsEmpty()
    {
        ReplyWithToolCall("""{"prompt":"a fox"}""");

        var result = await _service.SendUserMessageAsync(_session.Id, "a fox", TestContext.Current.CancellationToken);
        await _generations.WhenIdleAsync();

        Assert.Equal("", result.AssistantMessage.Text);
        Assert.NotNull(result.AssistantMessage.RunId);
    }

    [Fact]
    public async Task SendUserMessageAsync_ToolCallWithoutPrompt_SavesErrorAndStartsNoRun()
    {
        ReplyWithToolCall("{}");

        var result = await _service.SendUserMessageAsync(_session.Id, "a fox", TestContext.Current.CancellationToken);

        Assert.Null(result.Run);
        Assert.True(result.AssistantMessage.IsError);
        Assert.Equal(ErrorKind.InvalidToolCall, result.AssistantMessage.ErrorKind);
        Assert.Equal("", result.AssistantMessage.Text);
        Assert.Empty(_storedRuns);
        await _runs.DidNotReceive().AddRunAsync(Arg.Any<Run>(), Arg.Any<IReadOnlyList<Generation>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendUserMessageAsync_EmptyReply_SavesError()
    {
        ReplyWith(new AssistantChatResult("  ", [], null));

        var result = await _service.SendUserMessageAsync(_session.Id, "a fox", TestContext.Current.CancellationToken);

        Assert.True(result.AssistantMessage.IsError);
        Assert.Equal(ErrorKind.EmptyResponse, result.AssistantMessage.ErrorKind);
        Assert.Equal("", result.AssistantMessage.Text);
    }

    [Fact]
    public async Task SendUserMessageAsync_NoImageModels_SavesErrorAndKeepsUserMessage()
    {
        _session.Settings.ImageModels = [];
        ReplyWithToolCall("""{"prompt":"a fox"}""");

        var result = await _service.SendUserMessageAsync(_session.Id, "a fox", TestContext.Current.CancellationToken);

        Assert.Null(result.Run);
        Assert.True(result.AssistantMessage.IsError);
        Assert.Equal($"Session {_session.Id} has no image models.", result.AssistantMessage.Text);
        Assert.Equal("a fox", result.UserMessage.Text);
        Assert.Equal(2, _stored.Count);
    }

    [Fact]
    public async Task SendUserMessageAsync_ProviderRateLimited_SavesUserAndErrorMessage()
    {
        _chat.CompleteChatAsync(Arg.Any<AssistantChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AssistantChatResult>(
                new ProviderException(ErrorKind.RateLimited, "Rate limit exceeded")));

        var result = await _service.SendUserMessageAsync(_session.Id, "a fox", TestContext.Current.CancellationToken);

        Assert.Null(result.Run);
        Assert.Equal("a fox", result.UserMessage.Text);
        Assert.Equal(2, _stored.Count);
        Assert.Equal(ChatRole.Assistant, result.AssistantMessage.Role);
        Assert.True(result.AssistantMessage.IsError);
        Assert.Equal(ErrorKind.RateLimited, result.AssistantMessage.ErrorKind);
        Assert.Equal("Rate limit exceeded", result.AssistantMessage.Text);
    }

    [Fact]
    public async Task SendUserMessageAsync_EmptyText_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SendUserMessageAsync(_session.Id, "  ", TestContext.Current.CancellationToken));
        Assert.Empty(_stored);
    }

    [Fact]
    public async Task SendUserMessageAsync_UnknownSession_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SendUserMessageAsync(Guid.NewGuid(), "a fox", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ReadPrompt_ValidArguments_ReturnsTrimmedPrompt()
    {
        Assert.Equal("a fox", AssistantService.ReadPrompt("""{"prompt":"  a fox  "}"""));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("""{"prompt":""}""")]
    [InlineData("""{"prompt":"   "}""")]
    [InlineData("""{"prompt":3}""")]
    [InlineData("{\"prompt\":\"a fox\"")]
    [InlineData("[]")]
    public void ReadPrompt_InvalidArguments_ReturnsNull(string json)
    {
        Assert.Null(AssistantService.ReadPrompt(json));
    }
}
