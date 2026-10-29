using ImageGen.Core.Context;
using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Core.Tests.Services;

public class ConversationSummaryServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly ModelRef Assistant = new(ProviderIds.OpenRouter, "vendor/assistant");

    private readonly ISessionRepository _sessions = Substitute.For<ISessionRepository>();
    private readonly IChatMessageRepository _messages = Substitute.For<IChatMessageRepository>();
    private readonly IRunRepository _runs = Substitute.For<IRunRepository>();
    private readonly IConversationSummaryRepository _summaries = Substitute.For<IConversationSummaryRepository>();
    private readonly IProviderRouter _providers = Substitute.For<IProviderRouter>();
    private readonly IAssistantChatClient _chat = Substitute.For<IAssistantChatClient>();
    private readonly Session _session = new()
    {
        Id = Guid.NewGuid(),
        Title = "Fox",
        CreatedAt = Now,
        UpdatedAt = Now,
        Settings = new SessionSettings
        {
            ImageModels = [new ModelRef(ProviderIds.OpenRouter, "vendor/image")],
            RunsPerModel = 1,
            AssistantModel = Assistant,
        },
    };
    private readonly List<ChatMessage> _history;
    private readonly ConversationSummaryService _service;

    public ConversationSummaryServiceTests()
    {
        // 60 messages of 2000 characters (about 580 tokens each) are far more than the budget of a 32 000-token model.
        _history = Enumerable.Range(0, 60).Select(i => Message(i, new string('x', 2000))).ToList();
        _sessions.GetAsync(_session.Id, Arg.Any<CancellationToken>()).Returns(_session);
        _messages.ListBySessionAsync(_session.Id, Arg.Any<CancellationToken>()).Returns(_ => _history.ToList());
        _runs.ListRunsBySessionAsync(_session.Id, Arg.Any<CancellationToken>()).Returns([]);
        _summaries.GetAsync(_session.Id, Arg.Any<CancellationToken>()).Returns((ConversationSummary?)null);
        _providers.GetChatClient(ProviderIds.OpenRouter).Returns(_chat);
        _chat.ListChatModelsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<ChatModel>>([]));
        _chat.CompleteChatAsync(Arg.Any<AssistantChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AssistantChatResult("Summary text", [], 0.002m));
        _service = new ConversationSummaryService(
            _sessions,
            _messages,
            _runs,
            _summaries,
            _providers,
            new ChatModelCatalogService(_providers, NullLogger<ChatModelCatalogService>.Instance),
            new TokenCorrection(),
            TimeProvider.System,
            NullLogger<ConversationSummaryService>.Instance);
    }

    private ChatMessage Message(int index, string text) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = _session.Id,
        Role = index % 2 == 0 ? ChatRole.User : ChatRole.Assistant,
        Text = text,
        CreatedAt = Now.AddMinutes(index),
    };

    [Fact]
    public async Task Request_LongHistory_SavesSummaryCoveringOlderPart()
    {
        _service.Request(_session.Id);
        await _service.WhenIdleAsync();

        await _summaries.Received(1).UpsertAsync(
            Arg.Is<ConversationSummary>(summary =>
                summary.Text == "Summary text"
                && summary.CoversUpToMessageId == _history[39].Id
                && summary.Model == Assistant
                && summary.CostUsd == 0.002m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Request_LongHistory_SendsInstructionBeforeMessages()
    {
        _service.Request(_session.Id);
        await _service.WhenIdleAsync();

        await _chat.Received(1).CompleteChatAsync(
            Arg.Is<AssistantChatRequest>(request =>
                request.Messages.Count == 41
                && request.Messages[0].Text == AssistantPrompts.SummaryInstruction
                && request.Tools.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Request_ShortHistory_DoesNotCallModel()
    {
        _history.RemoveRange(10, 50);

        _service.Request(_session.Id);
        await _service.WhenIdleAsync();

        await _chat.DidNotReceive().CompleteChatAsync(Arg.Any<AssistantChatRequest>(), Arg.Any<CancellationToken>());
        await _summaries.DidNotReceive().UpsertAsync(Arg.Any<ConversationSummary>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Request_ModelFails_SavesNothingAndDoesNotThrow()
    {
        _chat.CompleteChatAsync(Arg.Any<AssistantChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AssistantChatResult>(new InvalidOperationException("provider is down")));

        _service.Request(_session.Id);
        await _service.WhenIdleAsync();

        await _summaries.DidNotReceive().UpsertAsync(Arg.Any<ConversationSummary>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Request_WhileRunning_DoesNotStartAnotherJob()
    {
        var gate = new TaskCompletionSource<AssistantChatResult>();
        _chat.CompleteChatAsync(Arg.Any<AssistantChatRequest>(), Arg.Any<CancellationToken>()).Returns(gate.Task);

        _service.Request(_session.Id);
        _service.Request(_session.Id);
        gate.SetResult(new AssistantChatResult("Summary text", [], null));
        await _service.WhenIdleAsync();

        await _chat.Received(1).CompleteChatAsync(Arg.Any<AssistantChatRequest>(), Arg.Any<CancellationToken>());
    }
}
