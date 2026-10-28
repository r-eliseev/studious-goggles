namespace ImageGen.Avalonia.Services.Navigation;

/// <summary>Splits a Shell-like route (<c>chat?sessionId=…</c>) into the path and the query parameters.</summary>
public static class RouteParser
{
    public static (string Path, IReadOnlyDictionary<string, string> Query) Parse(string route)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);

        var index = route.IndexOf('?', StringComparison.Ordinal);
        var path = index < 0 ? route : route[..index];
        var query = new Dictionary<string, string>(StringComparer.Ordinal);
        if (index >= 0)
        {
            foreach (var pair in route[(index + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var equals = pair.IndexOf('=', StringComparison.Ordinal);
                if (equals <= 0)
                {
                    continue;
                }

                query[Uri.UnescapeDataString(pair[..equals])] = Uri.UnescapeDataString(pair[(equals + 1)..]);
            }
        }

        return (path, query);
    }
}
