using System.Net;
using System.Text;
using System.Text.Json;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.OpenRouter;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Tests.TestDoubles;

namespace ImageGen.Core.Tests.OpenRouter;

public sealed class OpenRouterAudioTests
{
    private const string ApiKey = "sk-or-test-audio";
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ListAudioModelsAsync_Catalog_MapsEveryAudioAndSpeechModel()
    {
        const string json = """
            {"data":[
              {"id":"chat/one","name":"Chat One","description":"audio","architecture":{"output_modalities":["text","audio"]},"pricing":{"prompt":"0.01"}},
              {"id":"chat/both","architecture":{"output_modalities":["speech","audio"]}},
              {"id":"speech/one","name":"Speech One","architecture":{"output_modalities":["text","speech"]},"pricing":{"prompt":"0.00002"}},
              {"id":"speech/two","architecture":{"output_modalities":["speech"]},"pricing":{"prompt":"bad"}},
              {"id":"text/only","architecture":{"output_modalities":["text"]}},
              {"name":"missing id","architecture":{"output_modalities":["audio"]}}
            ]}
            """;
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var models = await client.ListAudioModelsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["chat/one", "chat/both", "speech/one", "speech/two"], models.Select(model => model.Id));
        Assert.Equal(AudioOutput.ChatAudio, models[1].Output);
        Assert.Equal(0.00002m, models[2].PricePerInputCharacterUsd);
        Assert.Null(models[0].PricePerInputCharacterUsd);
        Assert.Null(models[3].PricePerInputCharacterUsd);
        Assert.Equal(Now, models[0].FetchedAt);
        Assert.Equal("https://openrouter.ai/api/v1/models?output_modalities=all", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task GenerateAudioAsync_ChatAudioRetriesOneBadFormatWithWav()
    {
        var formats = new List<string>();
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            formats.Add(body.RootElement.GetProperty("audio").GetProperty("format").GetString()!);
            return formats.Count == 1
                ? new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("unsupported audio format") }
                : AudioEventResponse("RIFF\0\0\0\0WAVE"u8.ToArray());
        });
        var client = CreateClient(handler);

        await using var response = await client.GenerateAudioAsync(
            new AudioGenerationRequest(Model("lyria", AudioOutput.ChatAudio), "gentle music", null),
            TestContext.Current.CancellationToken);
        Assert.Equal("application/octet-stream", response.MediaType);
        using var output = new MemoryStream();
        await response.Content.CopyToAsync(output, TestContext.Current.CancellationToken);

        Assert.Equal(["mp3", "wav"], formats);
        Assert.Equal("audio/wav", response.MediaType);
        Assert.Equal("RIFF\0\0\0\0WAVE"u8.ToArray(), output.ToArray());
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task GenerateAudioAsync_SpeechStreamsBodyAndOmitsMissingVoice()
    {
        var speechAudio = new byte[2048];
        "ID3"u8.CopyTo(speechAudio);
        new Random(1234).NextBytes(speechAudio.AsSpan(3));
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(speechAudio),
        });
        var client = CreateClient(handler);

        await using var response = await client.GenerateAudioAsync(
            new AudioGenerationRequest(Model("speech/model", AudioOutput.Speech), "Say hello", null),
            TestContext.Current.CancellationToken);
        using var body = JsonDocument.Parse(handler.RequestBodies[0]);
        using var output = new MemoryStream();
        await response.Content.CopyToAsync(output, TestContext.Current.CancellationToken);

        Assert.Equal("https://openrouter.ai/api/v1/audio/speech", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal("speech/model", body.RootElement.GetProperty("model").GetString());
        Assert.Equal("Say hello", body.RootElement.GetProperty("input").GetString());
        Assert.Equal("mp3", body.RootElement.GetProperty("response_format").GetString());
        Assert.False(body.RootElement.TryGetProperty("voice", out _));
        Assert.Equal("audio/mpeg", response.MediaType);
        Assert.Equal(speechAudio, output.ToArray());
    }

    [Fact]
    public async Task GenerateAudioAsync_SpeechVoiceFailureKeepsProviderMessage()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.BadRequest, "{\"error\":{\"message\":\"voice is not supported\"}}");
        var client = CreateClient(handler);

        var error = await Assert.ThrowsAsync<ProviderException>(() => client.GenerateAudioAsync(
            new AudioGenerationRequest(Model("speech/model", AudioOutput.Speech), "Say hello", "voice-x"),
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.BadRequest, error.Kind);
        Assert.Equal("voice is not supported", error.Message);
    }

    private static HttpResponseMessage AudioEventResponse(byte[] audio)
    {
        var data = Convert.ToBase64String(audio);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"data: {{\"choices\":[{{\"delta\":{{\"audio\":{{\"data\":\"{data}\"}}}}}}]}}\ndata: [DONE]\n", Encoding.UTF8, "text/event-stream"),
        };
    }

    private static AudioModel Model(string id, AudioOutput output) => new()
    {
        ProviderId = ProviderIds.OpenRouter,
        Id = id,
        Name = id,
        Output = output,
        FetchedAt = Now,
    };

    private static OpenRouterClient CreateClient(StubHttpMessageHandler handler)
    {
        var secrets = Substitute.For<ISecretStore>();
        secrets.GetAsync(SecretNames.OpenRouterApiKey, Arg.Any<CancellationToken>()).Returns(ApiKey);
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(Now);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(ProviderIds.OpenRouter).Returns(new HttpClient(handler));
        var config = ProviderConfigLoader.Build(ProviderConfigLoader.ReadBuiltInJson(), null).Get(ProviderIds.OpenRouter);
        return new OpenRouterClient(config, factory, secrets, time);
    }
}
