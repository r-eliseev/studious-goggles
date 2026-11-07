using System.Globalization;
using System.Text.Json.Nodes;

namespace ImageGen.LiveCheck.Scenarios;

public sealed record EmbeddingTraceSummary(string Status, string Model, int ItemCount, string VectorLengths, string IndexPresent, string Usage)
{
    public static EmbeddingTraceSummary Parse(string traceLine)
    {
        var trace = JsonNode.Parse(traceLine) ?? throw new FormatException("Trace record is empty.");
        var fields = trace["fields"] ?? throw new FormatException("Trace fields are missing.");
        var body = JsonNode.Parse(fields["body"]?.GetValue<string>() ?? "{}") ?? new JsonObject();
        var data = body["data"] as JsonArray ?? [];
        var lengths = data.Select(item => (item?["embedding"] as JsonArray)?.Count.ToString(CultureInfo.InvariantCulture) ?? "missing");
        var usage = body["usage"]?.ToJsonString() ?? "unknown";
        return new EmbeddingTraceSummary(
            fields["status"]?.GetValue<string>() ?? "unknown",
            body["model"]?.GetValue<string>() ?? "missing",
            data.Count,
            string.Join(",", lengths),
            data.All(item => item is JsonObject row && row.ContainsKey("index")) ? "yes" : "no",
            usage);
    }
}
