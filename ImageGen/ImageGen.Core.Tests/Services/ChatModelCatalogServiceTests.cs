using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Core.Tests.Services;

public class ChatModelCatalogServiceTests
{
    private readonly IAssistantChatClient _chat = Substitute.For<IAssistantChatClient>();
    private readonly IProviderRouter _providers = Substitute.For<IProviderRouter>();
    private readonly ChatModelCatalogService _service;

    public ChatModelCatalogServiceTests()
    {
        _providers.GetChatClient(ProviderIds.OpenRouter).Returns(_chat);
        _chat.ListChatModelsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<ChatModel>>(
        [
            new ChatModel("vendor/vision", "Vision", SupportsImageInput: true, ContextLength: 1000),
            new ChatModel("vendor/text", "Text", SupportsImageInput: false, ContextLength: 1000),
        ]));
        _service = new ChatModelCatalogService(_providers, NullLogger<ChatModelCatalogService>.Instance);
    }

    [Fact]
    public async Task IsImageInputAsync_ModelWithImageInput_True()
    {
        Assert.True(await _service.IsImageInputAsync(new ModelRef(ProviderIds.OpenRouter, "vendor/vision"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsImageInputAsync_ModelWithoutImageInput_False()
    {
        Assert.False(await _service.IsImageInputAsync(new ModelRef(ProviderIds.OpenRouter, "vendor/text"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsImageInputAsync_UnknownModel_False()
    {
        Assert.False(await _service.IsImageInputAsync(new ModelRef(ProviderIds.OpenRouter, "vendor/missing"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsImageInputAsync_CatalogFails_FalseAndNoThrow()
    {
        _chat.ListChatModelsAsync(Arg.Any<CancellationToken>()).Returns<Task<IReadOnlyList<ChatModel>>>(_ => throw new ProviderException(ErrorKind.Network, "down"));
        var service = new ChatModelCatalogService(_providers, NullLogger<ChatModelCatalogService>.Instance);

        Assert.False(await service.IsImageInputAsync(new ModelRef(ProviderIds.OpenRouter, "vendor/vision"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListAssistantModelsAsync_ReturnsOnlyModelsWithToolsSortedByName()
    {
        _chat.ListChatModelsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<ChatModel>>(
        [
            new ChatModel("vendor/zeta", "zeta", SupportsImageInput: false, ContextLength: 1000, SupportsTools: true),
            new ChatModel("vendor/no-tools", "Alpha", SupportsImageInput: false, ContextLength: 1000),
            new ChatModel("vendor/alpha", "Alpha Two", SupportsImageInput: true, ContextLength: 1000, SupportsTools: true),
        ]));
        var service = new ChatModelCatalogService(_providers, NullLogger<ChatModelCatalogService>.Instance);

        var models = await service.ListAssistantModelsAsync(ProviderIds.OpenRouter, TestContext.Current.CancellationToken);

        Assert.Equal(["vendor/alpha", "vendor/zeta"], models.Select(m => m.Id));
    }

    [Fact]
    public async Task ListAssistantModelsAsync_CatalogFails_EmptyList()
    {
        _chat.ListChatModelsAsync(Arg.Any<CancellationToken>()).Returns<Task<IReadOnlyList<ChatModel>>>(_ => throw new ProviderException(ErrorKind.Network, "down"));
        var service = new ChatModelCatalogService(_providers, NullLogger<ChatModelCatalogService>.Instance);

        var models = await service.ListAssistantModelsAsync(ProviderIds.OpenRouter, TestContext.Current.CancellationToken);

        Assert.Empty(models);
    }

    [Fact]
    public async Task ListAllAssistantModelsAsync_CombinesProvidersAndSkipsFailedCatalog()
    {
        var other = Substitute.For<IAssistantChatClient>();
        _providers.ChatProviderIds.Returns([ProviderIds.OpenRouter, "other", "broken"]);
        _providers.GetChatClient("other").Returns(other);
        _providers.GetChatClient("broken").Returns(_chat);
        other.ListChatModelsAsync(Arg.Any<CancellationToken>()).Returns([
            new ChatModel("same", "Same", false, 200, SupportsTools: true),
        ]);
        _chat.ListChatModelsAsync(Arg.Any<CancellationToken>()).Returns<Task<IReadOnlyList<ChatModel>>>(
            _ => throw new ProviderException(ErrorKind.Network, "sensitive details"));
        var service = new ChatModelCatalogService(_providers, NullLogger<ChatModelCatalogService>.Instance);

        var models = await service.ListAllAssistantModelsAsync(TestContext.Current.CancellationToken);

        var model = Assert.Single(models);
        Assert.Equal("other", model.ProviderId);
        Assert.Equal("same", model.Model.Id);
    }

    [Fact]
    public async Task IsImageInputAsync_CalledTwice_LoadsCatalogOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var reference = new ModelRef(ProviderIds.OpenRouter, "vendor/vision");

        await _service.IsImageInputAsync(reference, ct);
        await _service.IsImageInputAsync(reference, ct);

        await _chat.Received(1).ListChatModelsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListAllAssistantModelsAsync_ForceRefresh_ReloadsCachedCatalog()
    {
        _providers.ChatProviderIds.Returns([ProviderIds.OpenRouter]);
        await _service.ListAllAssistantModelsAsync(forceRefresh: false, TestContext.Current.CancellationToken);
        await _service.ListAllAssistantModelsAsync(forceRefresh: true, TestContext.Current.CancellationToken);

        await _chat.Received(2).ListChatModelsAsync(Arg.Any<CancellationToken>());
    }
}
