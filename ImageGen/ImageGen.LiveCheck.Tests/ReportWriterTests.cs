using ImageGen.LiveCheck;
using ImageGen.LiveCheck.Report;

namespace ImageGen.LiveCheck.Tests;

public sealed class ReportWriterTests
{
    [Fact]
    public void Write_ContainsSecret_RefusesToPersistReport()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var writer = new ReportWriter(directory);
            var result = new CheckResult("provider", "scenario", "expected", "token-secret", "different", null, "—", "trace");

            Assert.Throws<InvalidOperationException>(() => writer.Write([result], new Dictionary<string, BudgetGuard>(), ["token-secret"], new DateOnly(2026, 1, 1)));
            Assert.False(File.Exists(Path.Combine(directory, "2026-01-01", "report.md")));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
