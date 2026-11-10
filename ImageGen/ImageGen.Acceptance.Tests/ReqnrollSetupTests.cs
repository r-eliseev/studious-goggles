using System.Text.Json;

namespace ImageGen.Acceptance.Tests;

public sealed class ReqnrollSetupTests
{
    [Fact]
    public void MissingOrPendingStepsOutcome_IsError()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "reqnroll.json")));

        var outcome = document.RootElement
            .GetProperty("runtime")
            .GetProperty("missingOrPendingStepsOutcome")
            .GetString();

        Assert.Equal("Error", outcome);
    }
}
