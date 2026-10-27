using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Sessions;
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
    private readonly IOpenRouterChatClient _client = Substitute.For<IOpenRouterChatClient>();
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
        var catalog = new ModelCatalogService(Substitute.For<IModelProvider>(), models);
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
            .Returns(Task.FromException<OpenRouterChatResult>(new ProviderException(ErrorKind.RateLimited, "slow down")));

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
