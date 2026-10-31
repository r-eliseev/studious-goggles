namespace ImageGen.LiveCheck;

public sealed record CheckResult(
    string Provider,
    string Scenario,
    string Expected,
    string Fact,
    string Status,
    string? Model,
    string Price,
    string Trace);
