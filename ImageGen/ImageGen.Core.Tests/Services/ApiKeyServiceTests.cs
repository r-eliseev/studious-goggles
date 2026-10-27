using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;

namespace ImageGen.Core.Tests.Services;

public class ApiKeyServiceTests
{
    private readonly ISecretStore _secrets = Substitute.For<ISecretStore>();
    private readonly IModelProvider _client = Substitute.For<IModelProvider>();
    private readonly ApiKeyService _service;

    public ApiKeyServiceTests()
    {
        _service = new ApiKeyService(_secrets, _client);
    }

    [Fact]
    public async Task SaveAsync_ValidKey_TrimsAndSaves()
    {
        var ct = TestContext.Current.CancellationToken;
        _client.CheckKeyAsync("sk-1", Arg.Any<CancellationToken>()).Returns(ApiKeyStatus.Valid);

        var status = await _service.SaveAsync("  sk-1  ", ct);

        Assert.Equal(ApiKeyStatus.Valid, status);
        await _secrets.Received(1).SetAsync(SecretNames.OpenRouterApiKey, "sk-1", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(ApiKeyStatus.Invalid)]
    [InlineData(ApiKeyStatus.NetworkError)]
    public async Task SaveAsync_NotValid_DoesNotSave(ApiKeyStatus checkResult)
    {
        var ct = TestContext.Current.CancellationToken;
        _client.CheckKeyAsync("sk-1", Arg.Any<CancellationToken>()).Returns(checkResult);

        var status = await _service.SaveAsync("sk-1", ct);

        Assert.Equal(checkResult, status);
        await _secrets.DidNotReceive().SetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("sk-1", true)]
    public async Task HasKeyAsync_StoredValue_ReturnsExpected(string? stored, bool expected)
    {
        _secrets.GetAsync(SecretNames.OpenRouterApiKey, Arg.Any<CancellationToken>()).Returns(stored);

        Assert.Equal(expected, await _service.HasKeyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetMaskedKeyAsync_StoredKey_ShowsOnlyLastFourCharacters()
    {
        _secrets.GetAsync(SecretNames.OpenRouterApiKey, Arg.Any<CancellationToken>()).Returns("sk-or-v1-abcdef1234");

        Assert.Equal("••••1234", await _service.GetMaskedKeyAsync(TestContext.Current.CancellationToken));
    }
}
