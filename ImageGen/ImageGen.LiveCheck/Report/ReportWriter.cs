using System.Globalization;
using System.Text;

namespace ImageGen.LiveCheck.Report;

public sealed class ReportWriter(string outputDirectory)
{
    public string Write(IReadOnlyList<CheckResult> results, IReadOnlyDictionary<string, BudgetGuard> budgets, IReadOnlyList<string> secrets, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(results);
        var day = Path.Combine(outputDirectory, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(day);
        var reportPath = Path.Combine(day, "report.md");
        var text = new StringBuilder()
            .AppendLine("# Live API check")
            .AppendLine()
            .AppendLine($"Date: {date:yyyy-MM-dd}")
            .AppendLine()
            .AppendLine("| Provider | Scenario | Expected | Fact | Status | Model | Price | Trace |")
            .AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (var result in results)
        {
            text.Append("| ").Append(Cell(result.Provider)).Append(" | ").Append(Cell(result.Scenario)).Append(" | ")
                .Append(Cell(result.Expected)).Append(" | ").Append(Cell(result.Fact)).Append(" | ").Append(Cell(result.Status)).Append(" | ")
                .Append(Cell(result.Model ?? "—")).Append(" | ").Append(Cell(result.Price)).Append(" | ").Append(Cell(result.Trace)).AppendLine(" |");
        }

        text.AppendLine().AppendLine("## Budget").AppendLine();
        foreach (var (provider, budget) in budgets.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            text.Append("- ").Append(provider).Append(": ").Append(budget.Requests).Append(" requests, $")
                .Append(budget.SpentUsd.ToString("0.####", CultureInfo.InvariantCulture)).Append(" estimated / reserved of $")
                .Append(budget.BudgetUsd.ToString("0.####", CultureInfo.InvariantCulture)).AppendLine();
        }

        var report = text.ToString();
        if (secrets.Where(secret => !string.IsNullOrEmpty(secret)).Any(secret => report.Contains(secret, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("The report contains a provider key; refusing to write it.");
        }

        File.WriteAllText(reportPath, report, new UTF8Encoding(false));
        return reportPath;
    }

    private static string Cell(string value) => value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');
}
