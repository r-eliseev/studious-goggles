using System.Net;
using System.Text;
using System.Text.Json;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.OpenAiCompatible;
using ImageGen.Core.OpenRouter;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Tests.TestDoubles;

namespace ImageGen.Core.Tests.Providers;

[Collection(ProviderResponseSizeCollection.Name)]
public sealed class WrappedImagePayloadTests
{
    [Fact]
    public async Task BothClients_AcceptWrappedPayloadBelowProductionLimit()
    {
        var payload = Convert.ToBase64String(new byte[49 * 1024 * 1024]);
        var wrapped = WrapBase64(payload);
        var json = JsonSerializer.Serialize(new { data = new[] { new { b64_json = wrapped } } });
        Assert.True(Encoding.UTF8.GetByteCount(json) < ProviderResponseLimits.GeneratedImageJson);
        var cancellationToken = TestContext.Current.CancellationToken;

        var router = CreateOpenRouterClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, json));
        var routerResult = await router.GenerateAsync(new ImageGenerationRequest("m/x", "p", null), cancellationToken);
        Assert.Equal(49L * 1024 * 1024, routerResult.Content.LongLength);

        var compatible = CreateOpenAiCompatibleClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, json));
        var compatibleResult = await compatible.GenerateAsync(new ImageGenerationRequest("sd-xl", "p", null), cancellationToken);
        Assert.Equal(49L * 1024 * 1024, compatibleResult.Content.LongLength);
    }

    [Fact]
    public async Task BothClients_InvalidBase64_KeepFormatException()
    {
        var json = JsonSerializer.Serialize(new { data = new[] { new { b64_json = "not base64" } } });
        var cancellationToken = TestContext.Current.CancellationToken;
        var router = CreateOpenRouterClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, json));
        var compatible = CreateOpenAiCompatibleClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, json));

        await Assert.ThrowsAsync<FormatException>(() => router.GenerateAsync(new ImageGenerationRequest("m/x", "p", null), cancellationToken));
        await Assert.ThrowsAsync<FormatException>(() => compatible.GenerateAsync(new ImageGenerationRequest("sd-xl", "p", null), cancellationToken));
    }

    [Fact]
    public async Task OpenAiCompatible_OneByteOverProductionImageLimit_MapsToUnknown()
    {
        var encoded = Convert.ToBase64String(new byte[ProviderResponseLimits.Image + 1]);
        var json = JsonSerializer.Serialize(new { data = new[] { new { b64_json = encoded } } });
        var client = CreateOpenAiCompatibleClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, json));

        var exception = await Assert.ThrowsAsync<ProviderException>(() => client.GenerateAsync(
            new ImageGenerationRequest("sd-xl", "p", null), TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.Unknown, exception.Kind);
    }

    private static string WrapBase64(string encoded)
    {
        var builder = new StringBuilder(encoded.Length + encoded.Length / 76 * 2);
        for (var offset = 0; offset < encoded.Length; offset += 76)
        {
            if (offset > 0) builder.Append("\r\n");
            builder.Append(encoded, offset, Math.Min(76, encoded.Length - offset));
        }
        return builder.ToString();
    }

    private static OpenRouterClient CreateOpenRouterClient(StubHttpMessageHandler handler)
    {
        var secrets = Substitute.For<ISecretStore>();
        secrets.GetAsync(SecretNames.OpenRouterApiKey, Arg.Any<CancellationToken>()).Returns("test-key");
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(ProviderIds.OpenRouter).Returns(new HttpClient(handler));
        var config = ProviderConfigLoader.Build(ProviderConfigLoader.ReadBuiltInJson(), null).Get(ProviderIds.OpenRouter);
        return new OpenRouterClient(config, factory, secrets, TimeProvider.System);
    }

    private static OpenAiCompatibleClient CreateOpenAiCompatibleClient(StubHttpMessageHandler handler)
    {
        const string providers = """{"providers":[{"id":"local","kind":"openAiCompatible","displayName":"Local","baseAddress":"http://localhost:1234/v1","imageModels":[{"id":"sd-xl","name":"SD XL","supportsSeed":true,"maxImagesPerRequest":1}]}]}""";
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("local").Returns(new HttpClient(handler));
        var secrets = Substitute.For<ISecretStore>();
        secrets.GetAsync("provider.local.api_key", Arg.Any<CancellationToken>()).Returns("test-key");
        var config = ProviderConfigLoader.Build(providers, null).Get("local");
        return new OpenAiCompatibleClient(config, factory, secrets, TimeProvider.System);
    }
}
