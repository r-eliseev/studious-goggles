using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Resources;
using ImageGen.Maui.Resources.Strings;

namespace ImageGen.Core.Tests.MauiLinked;

public class AppResourcesTests
{
    [Fact]
    public void Properties_MatchResxKeysExactly()
    {
        var resourceKeys = ReadResourceKeys().OrderBy(key => key, StringComparer.Ordinal).ToArray();
        var propertyNames = typeof(AppResources)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(resourceKeys, propertyNames);
    }

    [Fact]
    public void Properties_ReturnNonEmptyText()
    {
        foreach (var property in typeof(AppResources).GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            var text = (string?)property.GetValue(null);

            Assert.False(string.IsNullOrWhiteSpace(text), $"Empty text for {property.Name}");
        }
    }

    [Fact]
    public void AppTitle_IsImageGen()
    {
        Assert.Equal("ImageGen", AppResources.AppTitle);
    }

    private static List<string> ReadResourceKeys()
    {
        var manager = new ResourceManager("ImageGen.Maui.Resources.Strings.AppResources", typeof(AppResources).Assembly);
        using var set = manager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false)
            ?? throw new InvalidOperationException("AppResources.resx is not embedded.");

        var keys = new List<string>();
        foreach (DictionaryEntry entry in set)
        {
            keys.Add((string)entry.Key);
        }

        return keys;
    }
}
