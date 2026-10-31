using ImageGen.LiveCheck;
using ImageGen.LiveCheck.Scenarios;

try
{
    var options = LiveCheckOptions.Parse(args);
    var root = Path.GetFullPath(options.OutputDirectory);
    var app = new LiveCheckRunner(options, TimeProvider.System);
    await app.RunAsync(CancellationToken.None);
    Console.WriteLine($"Report written under {root}.");
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine("Usage: dotnet run --project ImageGen/ImageGen.LiveCheck -- [--openrouter-key <key>] [--fal-key <key>] [--together-key <key>] [--provider-key-<id> <key>] [--openai-compatible <providers.json>] [--budget-usd 1] [--max-requests 40] [--only openrouter,fal] [--out tmp/live-check]");
    Environment.ExitCode = 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Live check failed ({ex.GetType().Name}). See the safe HTTP trace for details.");
    Environment.ExitCode = 1;
}

internal sealed class HttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new();
}
