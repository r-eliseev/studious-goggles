using System.Text.Json;
using ImageGen.Core.Diagnostics;

namespace ImageGen.Presentation.Logging;

/// <summary>
/// Writes the debug trace (task 085, <c>docs/specs/debug-trace.md</c>) to <c>debug/&lt;date&gt;/trace.jsonl</c> in the data
/// folder: one JSON object per line. Pictures of a record are written next to it as files, and the line names them.
/// </summary>
public sealed class FileDebugTrace(string dataDirectory, TimeProvider timeProvider) : IDebugTrace
{
    public const string Schema = "debug-trace/1";

    private readonly Lock gate = new();

    public bool IsEnabled => true;

    public void Record(TraceRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var now = timeProvider.GetUtcNow();
        var dayDirectory = Path.Combine(dataDirectory, "debug", now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));

        lock (gate)
        {
            Directory.CreateDirectory(dayDirectory);
            var blobs = new List<object>(record.Blobs.Count);
            foreach (var blob in record.Blobs)
            {
                var file = $"{blob.Name}.{Extension(blob.MediaType)}";
                File.WriteAllBytes(Path.Combine(dayDirectory, file), blob.Content);
                blobs.Add(new { name = blob.Name, file, mediaType = blob.MediaType });
            }

            var line = JsonSerializer.Serialize(new
            {
                schema = Schema,
                time = now.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                kind = record.Kind,
                fields = record.Fields,
                blobs,
            });
            File.AppendAllText(Path.Combine(dayDirectory, "trace.jsonl"), line + "\n");
        }
    }

    private static string Extension(string mediaType) => mediaType switch
    {
        "image/png" => "png",
        "image/jpeg" => "jpg",
        "image/webp" => "webp",
        "image/gif" => "gif",
        _ => "bin",
    };
}
