namespace ImageGen.Presentation.Tests.Architecture;

public class PresentationDependencyTests
{
    private static readonly string[] ForbiddenPrefixes =
    [
        "Microsoft.Maui",
        "Avalonia",
        "Microsoft.AspNetCore",
    ];

    [Fact]
    public void PresentationAssembly_ReferencedAssemblies_DoNotIncludeUiFrameworks()
    {
        var referenced = typeof(Routes).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        var forbidden = referenced
            .Where(name => ForbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(forbidden);
    }
}
