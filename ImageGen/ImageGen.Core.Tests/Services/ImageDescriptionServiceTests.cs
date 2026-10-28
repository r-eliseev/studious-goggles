using ImageGen.Core.Domain.Descriptions;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Core.Tests.Services;

public class ImageDescriptionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
    private const string Vision = "vendor/vision";

    private readonly ISessionRepository _sessions = Substitute.For<ISessionRepository>();
    private readonly IRunRepository _runs = Substitute.For<IRunRepository>();
    private readonly IProviderRouter _providers = Substitute.For<IProviderRouter>();
    private readonly IAssistantChatClient _chat = Substitute.For<IAssistantChatClient>();
    private readonly IImageFileStore _files = Substitute.For<IImageFileStore>();
    private readonly IImageDescriptionRepository _descriptions = Substitute.For<IImageDescriptionRepository>();
    private readonly Session _session;
    private readonly Generation _generation;
    private readonly ImageDescriptionService _service;

    public ImageDescriptionServiceTests()
    {
        _providers.GetChatClient(ProviderIds.OpenRouter).Returns(_chat);
        _chat.ListChatModelsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<ChatModel>>(
            [new ChatModel(Vision, "Vision", SupportsImageInput: true, ContextLength: 1000)]));
        _chat.CompleteChatAsync(Arg.Any<AssistantChatRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AssistantChatResult("A red fox in snow.", [], 0.0004m));

        _session = new Session
        {
            Id = Guid.NewGuid(),
            Title = "Fox",
            CreatedAt = Now,
            UpdatedAt = Now,
            Settings = new SessionSettings
            {
                ImageModels = [new ModelRef(ProviderIds.OpenRouter, SessionSettings.DefaultImageModelId)],
                AssistantModel = new ModelRef(ProviderIds.OpenRouter, Vision),
            },
        };
        _sessions.GetAsync(_session.Id, Arg.Any<CancellationToken>()).Returns(_session);

        _generation = new Generation
        {
            Id = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            SessionId = _session.Id,
            ProviderId = ProviderIds.OpenRouter,
            ModelId = SessionSettings.DefaultImageModelId,
            Prompt = "fox",
            Seed = 1,
            SeedApplied = true,
            Status = GenerationStatus.Succeeded,
            ImagePath = "images/s/g.png",
            MediaType = "image/png",
            CreatedAt = Now,
            SortOrder = 0,
        };
        _runs.GetGenerationAsync(_generation.Id, Arg.Any<CancellationToken>()).Returns(_generation);
        _files.ReadAsync("images/s/g.png", Arg.Any<CancellationToken>()).Returns([1, 2, 3]);

        var chatModels = new ChatModelCatalogService(_providers, NullLogger<ChatModelCatalogService>.Instance);
        _service = new ImageDescriptionService(_sessions, _runs, _providers, chatModels, _files, _descriptions, TimeProvider.System);
    }

    [Fact]
    public async Task DescribeAsync_ModelWithImages_OneCallWithImageAndAssistantModel()
    {
        var description = await _service.DescribeAsync(_session.Id, _generation.Id, DescriptionMode.Description, "English", TestContext.Current.CancellationToken);

        Assert.Equal("A red fox in snow.", description.Text);
        Assert.Equal(Vision, description.ModelId);
        Assert.Equal(ProviderIds.OpenRouter, description.ProviderId);
        Assert.Equal(0.0004m, description.CostUsd);
        var request = Assert.Single(_chat.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(IAssistantChatClient.CompleteChatAsync))
            .Select(c => (AssistantChatRequest)c.GetArguments()[0]!));
        Assert.Equal(Vision, request.Model);
        Assert.Equal("data:image/png;base64,AQID", Assert.Single(request.Messages.Single(m => m.Role == AssistantChatMessage.UserRole).ImageDataUrls));
        await _descriptions.Received(1).AddAsync(description, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DescribeAsync_AssistantWithoutImageInput_ThrowsAndSendsNothing()
    {
        _chat.ListChatModelsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<ChatModel>>([]));

        var ex = await Assert.ThrowsAsync<ImageDescriptionException>(
            () => _service.DescribeAsync(_session.Id, _generation.Id, DescriptionMode.Description, "English", TestContext.Current.CancellationToken));

        Assert.Equal(DescriptionError.ModelDoesNotAcceptImages, ex.Error);
        await _chat.DidNotReceive().CompleteChatAsync(Arg.Any<AssistantChatRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DescribeAsync_ImageFileMissing_ThrowsSourceMissingBeforeTheRequest()
    {
        _files.ReadAsync("images/s/g.png", Arg.Any<CancellationToken>()).Returns<byte[]>(_ => throw new FileNotFoundException("gone"));

        var ex = await Assert.ThrowsAsync<ImageDescriptionException>(
            () => _service.DescribeAsync(_session.Id, _generation.Id, DescriptionMode.Description, "English", TestContext.Current.CancellationToken));

        Assert.Equal(DescriptionError.SourceMissing, ex.Error);
        await _chat.DidNotReceive().CompleteChatAsync(Arg.Any<AssistantChatRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DescribeAsync_EmptyReply_ThrowsAndSavesNothing()
    {
        _chat.CompleteChatAsync(Arg.Any<AssistantChatRequest>(), Arg.Any<CancellationToken>()).Returns(new AssistantChatResult("  ", [], null));

        var ex = await Assert.ThrowsAsync<ImageDescriptionException>(
            () => _service.DescribeAsync(_session.Id, _generation.Id, DescriptionMode.Description, "English", TestContext.Current.CancellationToken));

        Assert.Equal(DescriptionError.EmptyResponse, ex.Error);
        await _descriptions.DidNotReceive().AddAsync(Arg.Any<ImageDescription>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DescribeAsync_ProviderError_PropagatesAndSavesNothing()
    {
        _chat.CompleteChatAsync(Arg.Any<AssistantChatRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<AssistantChatResult>>(_ => throw new ProviderException(ErrorKind.RateLimited, "slow down"));

        await Assert.ThrowsAsync<ProviderException>(
            () => _service.DescribeAsync(_session.Id, _generation.Id, DescriptionMode.Description, "English", TestContext.Current.CancellationToken));

        await _descriptions.DidNotReceive().AddAsync(Arg.Any<ImageDescription>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DescribeAsync_PromptMode_UsesPromptSystemText()
    {
        await _service.DescribeAsync(_session.Id, _generation.Id, DescriptionMode.Prompt, "English", TestContext.Current.CancellationToken);

        var request = (AssistantChatRequest)_chat.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(IAssistantChatClient.CompleteChatAsync)).GetArguments()[0]!;
        Assert.Equal(DescriptionPrompts.For(DescriptionMode.Prompt, "English"), request.Messages[0].Text);
        Assert.NotEqual(DescriptionPrompts.For(DescriptionMode.Description, "English"), request.Messages[0].Text);
    }

    [Fact]
    public async Task DescribeAsync_NoCostReturned_SavesNullCost()
    {
        _chat.CompleteChatAsync(Arg.Any<AssistantChatRequest>(), Arg.Any<CancellationToken>()).Returns(new AssistantChatResult("Fox.", [], null));

        var description = await _service.DescribeAsync(_session.Id, _generation.Id, DescriptionMode.Description, "English", TestContext.Current.CancellationToken);

        Assert.Null(description.CostUsd);
    }

    [Fact]
    public void DescriptionPrompts_LanguageIsPartOfDescriptionOnly()
    {
        Assert.Contains("Russian", DescriptionPrompts.For(DescriptionMode.Description, "Russian"), StringComparison.Ordinal);
        Assert.Contains("English", DescriptionPrompts.For(DescriptionMode.Prompt, "Russian"), StringComparison.Ordinal);
    }
}
