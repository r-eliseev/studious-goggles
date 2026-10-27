namespace ImageGen.Data;

/// <summary>Ordered schema migrations. Never edit a released migration; append a new one.</summary>
internal static class Migrations
{
    /// <summary>Scripts in order. Each one is an embedded <c>Migrations/*.sql</c> file.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        ReadScript("0001_initial.sql"),
    ];

    private static string ReadScript(string fileName)
    {
        using var stream = typeof(Migrations).Assembly.GetManifestResourceStream(fileName)
            ?? throw new InvalidOperationException($"Migration script '{fileName}' is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
