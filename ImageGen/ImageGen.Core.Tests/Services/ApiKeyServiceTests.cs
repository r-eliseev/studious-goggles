using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Core.Tests.TestDoubles;

namespace ImageGen.Core.Tests.Services;

public class ApiKeyServiceTests
{
    private readonly ISecretStore _secrets = Substitute.For<ISecretStore>();
    private readonly IProviderRouter _providers = Substitute.For<IProviderRouter>();
    private readonly IProviderConnection _connection = Substitute.For<IProviderConnection>();
    private readonly CapturingLogger<ApiKeyService> _logger = new();
    private readonly ApiKeyService _service;

    public ApiKeyServiceTests()
    {
        _providers.GetConnection(ProviderIds.OpenRouter).Returns(_connection);
        _service = new ApiKeyService(_providers, _secrets, _logger);
    }

    [Fact]
    public async Task SaveAsync_ValidKey_TrimsAndSavesUnderOpenRouterName()
    {
        var ct = TestContext.Current.CancellationToken;
        _connection.CheckKeyAsync("sk-1", Arg.Any<CancellationToken>()).Returns(ApiKeyStatus.Valid);

        var status = await _service.SaveAsync(ProviderIds.OpenRouter, "  sk-1  ", ct);

        Assert.Equal(ApiKeyStatus.Valid, status);
        await _secrets.Received(1).SetAsync(SecretNames.OpenRouterApiKey, "sk-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_ValidKey_LogsResultWithoutTheKey()
    {
        var ct = TestContext.Current.CancellationToken;
        _connection.CheckKeyAsync("sk-secret-1", Arg.Any<CancellationToken>()).Returns(ApiKeyStatus.Valid);

        await _service.SaveAsync(ProviderIds.OpenRouter, "sk-secret-1", ct);

        Assert.Contains("API key check for provider openrouter: Valid", _logger.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-secret-1", _logger.AllText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ApiKeyStatus.Invalid)]
    [InlineData(ApiKeyStatus.NetworkError)]
    public async Task SaveAsync_NotValid_DoesNotSave(ApiKeyStatus checkResult)
    {
        var ct = TestContext.Current.CancellationToken;
        _connection.CheckKeyAsync("sk-1", Arg.Any<CancellationToken>()).Returns(checkResult);

        var status = await _service.SaveAsync(ProviderIds.OpenRouter, "sk-1", ct);

        Assert.Equal(checkResult, status);
        await _secrets.DidNotReceive().SetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_OtherProvider_ChecksWithThatProviderAndUsesItsSecretName()
    {
        var ct = TestContext.Current.CancellationToken;
        var local = Substitute.For<IProviderConnection>();
        _providers.GetConnection("local").Returns(local);
        local.CheckKeyAsync("k", Arg.Any<CancellationToken>()).Returns(ApiKeyStatus.Valid);

        await _service.SaveAsync("local", "k", ct);

        await local.Received(1).CheckKeyAsync("k", Arg.Any<CancellationToken>());
        await _secrets.Received(1).SetAsync("provider.local.api_key", "k", Arg.Any<CancellationToken>());
        await _secrets.DidNotReceive().SetAsync(SecretNames.OpenRouterApiKey, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("sk-1", true)]
    public async Task HasKeyAsync_StoredValue_ReturnsExpected(string? stored, bool expected)
    {
        _secrets.GetAsync(SecretNames.OpenRouterApiKey, Arg.Any<CancellationToken>()).Returns(stored);

        Assert.Equal(expected, await _service.HasKeyAsync(ProviderIds.OpenRouter, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HasKeyAsync_OtherProviderWithoutKey_DoesNotReadOpenRouterKey()
    {
        _secrets.GetAsync("provider.fal.api_key", Arg.Any<CancellationToken>()).Returns((string?)null);
        _secrets.GetAsync(SecretNames.OpenRouterApiKey, Arg.Any<CancellationToken>()).Returns("sk-or");

        Assert.False(await _service.HasKeyAsync("fal", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetMaskedKeyAsync_StoredKey_ShowsOnlyLastFourCharacters()
    {
        _secrets.GetAsync(SecretNames.OpenRouterApiKey, Arg.Any<CancellationToken>()).Returns("sk-or-v1-abcdef1234");

        Assert.Equal("••••1234", await _service.GetMaskedKeyAsync(ProviderIds.OpenRouter, TestContext.Current.CancellationToken));
    }
}
