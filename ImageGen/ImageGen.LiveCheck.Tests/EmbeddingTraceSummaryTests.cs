using ImageGen.LiveCheck.Scenarios;

namespace ImageGen.LiveCheck.Tests;

public sealed class EmbeddingTraceSummaryTests
{
    [Fact]
    public void Parse_EmbeddingTraceFixture_ReportsSafeResponseShapeAndUsage()
    {
        const string fixture = """
            {"kind":"http-response","fields":{"status":"200","path":"/api/v1/embeddings","body":"{\"model\":\"baai/bge-m3\",\"data\":[{\"index\":0,\"embedding\":[0.1,0.2]},{\"index\":1,\"embedding\":[0.3,0.4]}],\"usage\":{\"prompt_tokens\":7,\"total_tokens\":7,\"cost\":0.000001}}"}}
            """;

        var result = EmbeddingTraceSummary.Parse(fixture);

        Assert.Equal("200", result.Status);
        Assert.Equal("baai/bge-m3", result.Model);
        Assert.Equal(2, result.ItemCount);
        Assert.Equal("2,2", result.VectorLengths);
        Assert.Equal("yes", result.IndexPresent);
        Assert.Contains("cost", result.Usage, StringComparison.Ordinal);
    }
}
