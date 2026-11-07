using System.Net;
using System.Text;
using System.Text.Json;
using ImageGen.Core.Diagnostics;
using ImageGen.LiveCheck.Scenarios;

namespace ImageGen.LiveCheck.Tests;

public sealed class MusicScenarioTests
{
    [Fact]
    public void CollectSseLines_AudioChunks_WritesBytesAndTracesLengthsOnly()
    {
        var lines = new[]
        {
            "data: {\"choices\":[{\"delta\":{\"audio\":{\"data\":\"AQID\"}}}]}",
            "data: {\"choices\":[{\"delta\":{\"audio\":{\"data\":\"BAU=\",\"transcript\":\"piano\"}}}]}",
            "data: {\"choices\":[],\"usage\":{\"cost\":0.04,\"output_tokens\":5}}",
        };
        using var output = new MemoryStream();
        var trace = new List<TraceRecord>();

        var result = MusicScenario.CollectSseLines(lines, output, trace.Add);

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, output.ToArray());
        Assert.Equal(5, result.AudioBytes);
        Assert.True(result.HasTranscript);
        Assert.Equal(3, result.EventCount);
        Assert.Contains("0.04", result.UsageJson, StringComparison.Ordinal);
        Assert.Equal(2, trace.Count);
        Assert.DoesNotContain("AQID", string.Join(" ", trace.Select(record => string.Join(" ", record.Fields.Values))));
        Assert.DoesNotContain("BAU=", string.Join(" ", trace.Select(record => string.Join(" ", record.Fields.Values))));
    }

    [Fact]
    public void CollectSseLines_WithoutAudio_ThrowsReadableError()
    {
        using var output = new MemoryStream();

        var error = Assert.Throws<InvalidDataException>(() =>
            MusicScenario.CollectSseLines(["data: {\"choices\":[{\"delta\":{\"content\":\"ready\"}}]}", "data: [DONE]"], output));

        Assert.Contains("without audio", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(output.ToArray());
    }

    [Fact]
    public async Task RunAsync_FormatRejection_RetriesWithWavAndSavesFile()
    {
        var formats = new List<string>();
        var requestShapeIsCorrect = true;
        using var client = new HttpClient(new StubHandler(async (request, cancellationToken) =>
        {
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            var authorization = request.Headers.Authorization;
            formats.Add(root.GetProperty("audio").GetProperty("format").GetString()!);
            requestShapeIsCorrect &= authorization is { Scheme: "Bearer", Parameter: "test-key" }
                && root.GetProperty("model").GetString() == "google/lyria-3-clip-preview"
                && root.GetProperty("stream").GetBoolean()
                && root.GetProperty("modalities").EnumerateArray().Select(item => item.GetString()).SequenceEqual(["text", "audio"])
                && root.GetProperty("messages")[0].GetProperty("content").GetString() == "calm piano loop, 10 seconds";
            return formats.Count == 1
                ? new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("unsupported audio format") }
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("data: {\"choices\":[{\"delta\":{\"audio\":{\"data\":\"AQID\"}}}]}\ndata: [DONE]\n", Encoding.UTF8, "text/event-stream")
                };
        }));
        var outputDirectory = Path.Combine("tmp", "music-scenario-tests", Guid.NewGuid().ToString("N"));

        try
        {
            var result = await new MusicScenario().RunAsync(
                client,
                "test-key",
                outputDirectory,
                new DateOnly(2026, 11, 6),
                _ => { },
                () => true,
                TestContext.Current.CancellationToken);

            Assert.True(result.Succeeded);
            Assert.True(requestShapeIsCorrect);
            Assert.Equal(["mp3", "wav"], formats);
            Assert.EndsWith("music.wav", result.FilePath, StringComparison.Ordinal);
            Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(result.FilePath!, TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
