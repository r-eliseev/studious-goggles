using System.Reflection;
using ImageGen.E2E.Common;

namespace ImageGen.Presentation.Tests.E2E;

public sealed class AutomationIdsTests
{
    [Fact]
    public void EveryTestId_ExistsExactlyOnceInMauiMarkup()
    {
        var values = typeof(TestIds).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field.IsLiteral && !field.IsInitOnly)
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();
        var root = FindRepositoryRoot();
        var xamlFiles = Directory.GetFiles(Path.Combine(root, "ImageGen", "ImageGen.Maui", "Pages"), "*.xaml")
            .Concat(Directory.GetFiles(Path.Combine(root, "ImageGen", "ImageGen.Maui", "Controls"), "*.xaml"));
        var markup = xamlFiles.Select(File.ReadAllText).ToArray();

        Assert.Equal(values.Length, values.Distinct(StringComparer.Ordinal).Count());
        foreach (var value in values)
        {
            var occurrenceCount = markup.Sum(file => CountOccurrences(file, $"AutomationId=\"{value}\""));
            Assert.Equal(1, occurrenceCount);
        }
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ImageGen", "ImageGen.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find ImageGen.slnx above the test output directory.");
    }
}
