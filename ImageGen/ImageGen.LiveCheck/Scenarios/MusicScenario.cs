using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using ImageGen.Core.Diagnostics;
using ImageGen.Core.OpenRouter;

namespace ImageGen.LiveCheck.Scenarios;

public sealed class MusicScenario
{
    private const string Model = "google/lyria-3-clip-preview";
    private const string Prompt = "calm piano loop, 10 seconds";

    public async Task<MusicScenarioResult> RunAsync(
        HttpClient client,
        string apiKey,
        string outputDirectory,
        DateOnly date,
        Action<TraceRecord> recordTrace,
        Func<bool> reserveRetry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(recordTrace);
        ArgumentNullException.ThrowIfNull(reserveRetry);

        var dayDirectory = Path.Combine(outputDirectory, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(dayDirectory);
        var format = "mp3";
        for (var attempt = 0; attempt < 2; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (attempt == 1)
            {
                if (!reserveRetry())
                {
                    return new MusicScenarioResult(false, null, format, 0, 0, 0, false, null, null, null,
                        "wav retry skipped because the request cap is exhausted");
                }

                format = "wav";
            }

            using var request = CreateRequest(apiKey, format);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                RecordHttpResponse(recordTrace, format, response.StatusCode, mediaType);
                if (attempt == 0 && IsFormatOrVoiceError(error))
                {
                    continue;
                }

                return new MusicScenarioResult(false, null, format, 0, 0, 0, false, null, null, mediaType,
                    $"HTTP {(int)response.StatusCode}; provider rejected the {format} audio request");
            }

            RecordHttpResponse(recordTrace, format, response.StatusCode, mediaType);

            var finalPath = Path.Combine(dayDirectory, $"music.{format}");
            var temporaryPath = finalPath + ".partial";
            try
            {
                var summary = await CollectSseStreamAsync(
                    await response.Content.ReadAsStreamAsync(cancellationToken),
                    temporaryPath,
                    recordTrace,
                    cancellationToken);
                File.Move(temporaryPath, finalPath, overwrite: true);
                return new MusicScenarioResult(
                    true,
                    finalPath,
                    format,
                    new FileInfo(finalPath).Length,
                    summary.AudioBytes,
                    summary.EventCount,
                    summary.HasTranscript,
                    summary.UsageJson,
                    summary.CostUsd,
                    mediaType,
                    null);
            }
            catch
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }

                throw;
            }
        }

        throw new InvalidOperationException("The music scenario did not complete.");
    }

    public static MusicStreamSummary CollectSseLines(
        IEnumerable<string> lines,
        Stream output,
        Action<TraceRecord>? recordTrace = null)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(output);

        using var source = new MemoryStream(Encoding.UTF8.GetBytes(string.Join('\n', lines)));
        using var audio = new SseAudioStream(source);
        audio.CopyToAsync(output).GetAwaiter().GetResult();
        var summary = ToSummary(audio);
        RecordStreamTrace(recordTrace, summary, audio);
        return summary;
    }

    private static async Task<MusicStreamSummary> CollectSseStreamAsync(
        Stream response,
        string temporaryPath,
        Action<TraceRecord> recordTrace,
        CancellationToken cancellationToken)
    {
        await using var output = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await using var audio = new SseAudioStream(response, cancellationToken);
        await audio.CopyToAsync(output, cancellationToken);
        await output.FlushAsync(cancellationToken);
        var summary = ToSummary(audio);
        RecordStreamTrace(recordTrace, summary, audio);
        return summary;
    }

    private static MusicStreamSummary ToSummary(SseAudioStream audio) => new(
        checked((int)audio.AudioBytes),
        audio.EventCount,
        audio.HasTranscript,
        audio.UsageJson,
        audio.CostUsd);

    private static void RecordStreamTrace(Action<TraceRecord>? recordTrace, MusicStreamSummary summary, SseAudioStream audio)
    {
        if (summary.EventCount == 0)
        {
            return;
        }

        recordTrace?.Invoke(new TraceRecord("music-stream-event", new Dictionary<string, string?>
        {
            ["position"] = "first",
            ["index"] = "1",
            ["audioBytes"] = audio.FirstEventAudioBytes.ToString(CultureInfo.InvariantCulture),
            ["transcriptPresent"] = summary.HasTranscript.ToString(CultureInfo.InvariantCulture),
            ["usage"] = null,
        }, []));
        if (summary.EventCount > 1)
        {
            recordTrace?.Invoke(new TraceRecord("music-stream-event", new Dictionary<string, string?>
            {
                ["position"] = "last",
                ["index"] = summary.EventCount.ToString(CultureInfo.InvariantCulture),
                ["audioBytes"] = audio.LastEventAudioBytes.ToString(CultureInfo.InvariantCulture),
                ["transcriptPresent"] = summary.HasTranscript.ToString(CultureInfo.InvariantCulture),
                ["usage"] = summary.UsageJson,
            }, []));
        }
    }

    private static HttpRequestMessage CreateRequest(string apiKey, string format)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            model = Model,
            messages = new[] { new { role = "user", content = Prompt } },
            modalities = new[] { "text", "audio" },
            audio = new { format },
            stream = true,
        }), Encoding.UTF8, "application/json");
        return request;
    }

    private static bool IsFormatOrVoiceError(string body) =>
        body.Contains("voice", StringComparison.OrdinalIgnoreCase)
        || body.Contains("audio format", StringComparison.OrdinalIgnoreCase)
        || body.Contains("unsupported format", StringComparison.OrdinalIgnoreCase)
        || body.Contains("invalid format", StringComparison.OrdinalIgnoreCase);

    private static void RecordHttpResponse(Action<TraceRecord> recordTrace, string format, HttpStatusCode status, string? mediaType) =>
        recordTrace(new TraceRecord("music-http-response", new Dictionary<string, string?>
        {
            ["model"] = Model,
            ["format"] = format,
            ["status"] = ((int)status).ToString(CultureInfo.InvariantCulture),
            ["contentType"] = mediaType,
        }, []));

    public sealed record MusicStreamSummary(int AudioBytes, int EventCount, bool HasTranscript, string? UsageJson, decimal? CostUsd);

    public sealed record MusicScenarioResult(
        bool Succeeded,
        string? FilePath,
        string Format,
        long FileBytes,
        int AudioBytes,
        int EventCount,
        bool HasTranscript,
        string? UsageJson,
        decimal? CostUsd,
        string? ContentType,
        string? Error);
}
