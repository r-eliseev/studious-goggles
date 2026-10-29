namespace ImageGen.Core.Tests.Architecture;

public class CoreDependencyTests
{
    private static readonly string[] ForbiddenPrefixes =
    [
        "Microsoft.Maui",
        "Microsoft.AspNetCore",
        "Microsoft.Data.Sqlite",
        "Avalonia",
    ];

    [Fact]
    public void CoreAssembly_ReferencedAssemblies_DoNotIncludeUiOrStorage()
    {
        var referenced = typeof(CoreServiceCollectionExtensions).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        var forbidden = referenced
            .Where(name => ForbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(forbidden);
    }
}
