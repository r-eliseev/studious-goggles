using Avalonia.Controls;

namespace ImageGen.Avalonia.Services.Navigation;

/// <summary>Route path → factory of its page. The factory gets the query parameters of the route.</summary>
public sealed class PageRoutes
{
    private readonly Dictionary<string, Func<IReadOnlyDictionary<string, string>, Control>> _pages = new(StringComparer.Ordinal);

    public PageRoutes Add(string path, Func<IReadOnlyDictionary<string, string>, Control> create)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(create);
        _pages.Add(path, create);
        return this;
    }

    public Func<IReadOnlyDictionary<string, string>, Control>? Find(string path) => _pages.GetValueOrDefault(path);
}
