using System.Text;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.OpenRouter;
using ImageGen.Core.Providers;

namespace ImageGen.Core.Tests.OpenRouter;

public sealed class SseAudioStreamTests
{
    [Fact]
    public async Task ReadAsync_DecodesAudioAndCapturesTranscriptAndCost()
    {
        var input = string.Join('\n',
            "event: message",
            ": keep-alive",
            "data: {\"choices\":[{\"delta\":{\"audio\":{\"data\":\"SUQz\",\"transcript\":\"hello \"}}}]}",
            "data: {\"choices\":[{\"delta\":{\"audio\":{\"data\":\"AQI=\",\"transcript\":\"world\"}}}]}",
            "data: {\"choices\":[],\"usage\":{\"cost\":0.04}}",
            "data: [DONE]",
            string.Empty);
        await using var stream = new SseAudioStream(new MemoryStream(Encoding.UTF8.GetBytes(input)));
        using var output = new MemoryStream();
        await stream.CopyToAsync(output, TestContext.Current.CancellationToken);

        Assert.Equal(new byte[] { 0x49, 0x44, 0x33, 1, 2 }, output.ToArray());
        Assert.Equal("audio/mpeg", stream.MediaType);
        Assert.Equal("hello world", stream.Transcript);
        Assert.Equal(0.04m, stream.CostUsd);
    }

    [Fact]
    public async Task ReadAsync_WithoutAudio_ThrowsEmptyResponse()
    {
        var input = "data: {\"choices\":[{\"delta\":{\"content\":\"ready\"}}]}\ndata: [DONE]\n";
        await using var stream = new SseAudioStream(new MemoryStream(Encoding.UTF8.GetBytes(input)));

        var error = await Assert.ThrowsAsync<ProviderException>(async () => await stream.CopyToAsync(Stream.Null, TestContext.Current.CancellationToken));

        Assert.Equal(ErrorKind.EmptyResponse, error.Kind);
    }

    [Fact]
    public async Task ReadAsync_ProviderError_ThrowsProviderException()
    {
        var input = "data: {\"error\":{\"message\":\"model unavailable\"}}\n";
        await using var stream = new SseAudioStream(new MemoryStream(Encoding.UTF8.GetBytes(input)));

        var error = await Assert.ThrowsAsync<ProviderException>(async () => await stream.CopyToAsync(Stream.Null, TestContext.Current.CancellationToken));

        Assert.Contains("model unavailable", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAsync_WavHeader_ReportsWaveMediaType()
    {
        var wavHeader = Encoding.ASCII.GetBytes("RIFF\0\0\0\0WAVE");
        var input = $"data: {{\"choices\":[{{\"delta\":{{\"audio\":{{\"data\":\"{Convert.ToBase64String(wavHeader)}\"}}}}}}]}}\ndata: [DONE]\n";
        await using var stream = new SseAudioStream(new MemoryStream(Encoding.UTF8.GetBytes(input)));
        using var output = new MemoryStream();

        await stream.CopyToAsync(output, TestContext.Current.CancellationToken);

        Assert.Equal("audio/wav", stream.MediaType);
        Assert.Equal(wavHeader, output.ToArray());
    }

    [Fact]
    public async Task ReadAsync_MpegFrameHeader_ReportsMpegMediaType()
    {
        var frameHeader = new byte[] { 0xFF, 0xFB, 0x90, 0x64 };
        var input = $"data: {{\"choices\":[{{\"delta\":{{\"audio\":{{\"data\":\"{Convert.ToBase64String(frameHeader)}\"}}}}}}]}}\ndata: [DONE]\n";
        await using var stream = new SseAudioStream(new MemoryStream(Encoding.UTF8.GetBytes(input)));
        using var output = new MemoryStream();

        await stream.CopyToAsync(output, TestContext.Current.CancellationToken);

        Assert.Equal("audio/mpeg", stream.MediaType);
        Assert.Equal(frameHeader, output.ToArray());
    }

    [Fact]
    public async Task ReadAsync_LargeBase64Event_DecodesInPiecesWithoutChangingBytes()
    {
        var audio = new byte[200_000];
        "ID3"u8.CopyTo(audio);
        new Random(1234).NextBytes(audio.AsSpan(3));
        var input = $"data: {{\"choices\":[{{\"delta\":{{\"audio\":{{\"data\":\"{Convert.ToBase64String(audio)}\"}}}}}}]}}\ndata: [DONE]\n";
        await using var stream = new SseAudioStream(new MemoryStream(Encoding.UTF8.GetBytes(input)));
        using var output = new MemoryStream();

        await stream.CopyToAsync(output, TestContext.Current.CancellationToken);

        Assert.Equal(audio, output.ToArray());
        Assert.Equal(audio.Length, stream.AudioBytes);
    }
}
