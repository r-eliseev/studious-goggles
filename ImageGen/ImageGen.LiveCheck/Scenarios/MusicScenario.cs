using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using ImageGen.Core.Diagnostics;

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

        var accumulator = new SseAccumulator(output);
        foreach (var line in lines)
        {
            accumulator.ReadLine(line);
        }

        return accumulator.Complete(recordTrace);
    }

    private static async Task<MusicStreamSummary> CollectSseStreamAsync(
        Stream response,
        string temporaryPath,
        Action<TraceRecord> recordTrace,
        CancellationToken cancellationToken)
    {
        await using var output = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        using var reader = new StreamReader(response, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var accumulator = new SseAccumulator(output);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            accumulator.ReadLine(line);
        }

        await output.FlushAsync(cancellationToken);
        return accumulator.Complete(recordTrace);
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

    private sealed class SseAccumulator(Stream output)
    {
        private readonly Stream _output = output;
        private SseEventSummary? _first;
        private SseEventSummary? _last;
        private int _events;
        private int _audioBytes;
        private bool _hasTranscript;
        private string? _usageJson;
        private decimal? _costUsd;

        public void ReadLine(string line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                return;
            }

            var payload = line.AsSpan("data:".Length).Trim();
            if (payload.IsEmpty || payload.SequenceEqual("[DONE]"))
            {
                return;
            }

            using var document = JsonDocument.Parse(payload.ToString());
            var root = document.RootElement;
            var audioData = root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0
                && choices[0].TryGetProperty("delta", out var delta)
                && delta.TryGetProperty("audio", out var audio)
                && audio.TryGetProperty("data", out var data)
                ? data.GetString()
                : null;
            var decodedBytes = string.IsNullOrWhiteSpace(audioData) ? [] : Convert.FromBase64String(audioData);
            if (decodedBytes.Length > 0)
            {
                _output.Write(decodedBytes);
                _audioBytes += decodedBytes.Length;
            }

            var transcript = root.TryGetProperty("choices", out choices) && choices.GetArrayLength() > 0
                && choices[0].TryGetProperty("delta", out delta)
                && delta.TryGetProperty("audio", out audio)
                && audio.TryGetProperty("transcript", out var transcriptElement)
                && transcriptElement.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(transcriptElement.GetString());
            _hasTranscript |= transcript;
            if (root.TryGetProperty("usage", out var usage))
            {
                _usageJson = usage.GetRawText();
                if (usage.TryGetProperty("cost", out var cost) && cost.TryGetDecimal(out var parsedCost))
                {
                    _costUsd = parsedCost;
                }
            }

            var summary = new SseEventSummary(++_events, decodedBytes.Length, transcript, _usageJson);
            _first ??= summary;
            _last = summary;
        }

        public MusicStreamSummary Complete(Action<TraceRecord>? recordTrace)
        {
            if (_first is { } first)
            {
                RecordEvent(recordTrace, "first", first);
            }

            if (_last is { } last && !ReferenceEquals(_first, _last))
            {
                RecordEvent(recordTrace, "last", last);
            }

            if (_audioBytes == 0)
            {
                throw new InvalidDataException("The OpenRouter stream completed without audio data.");
            }

            return new MusicStreamSummary(_audioBytes, _events, _hasTranscript, _usageJson, _costUsd);
        }

        private static void RecordEvent(Action<TraceRecord>? recordTrace, string position, SseEventSummary summary)
        {
            recordTrace?.Invoke(new TraceRecord("music-stream-event", new Dictionary<string, string?>
            {
                ["position"] = position,
                ["index"] = summary.Index.ToString(CultureInfo.InvariantCulture),
                ["audioBytes"] = summary.AudioBytes.ToString(CultureInfo.InvariantCulture),
                ["transcriptPresent"] = summary.HasTranscript.ToString(CultureInfo.InvariantCulture),
                ["usage"] = summary.UsageJson,
            }, []));
        }
    }

    private sealed record SseEventSummary(int Index, int AudioBytes, bool HasTranscript, string? UsageJson);

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
