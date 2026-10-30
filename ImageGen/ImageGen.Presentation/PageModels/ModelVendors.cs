using ImageGen.Presentation.Resources.Strings;

namespace ImageGen.Presentation.PageModels;

public sealed record VendorGroup<T>(string Key, string Label, IReadOnlyList<T> Items);

/// <summary>Infers model-vendor groups from provider model ids and catalog display names.</summary>
public static class ModelVendors
{
    public const string OtherKey = "~other";

    public static string? PrefixOf(string providerId, string modelId)
    {
        var id = modelId.TrimStart('~');
        var slash = id.IndexOf('/');
        if (slash <= 0)
        {
            return null;
        }

        var prefix = id[..slash];
        if (string.Equals(providerId, "fal", StringComparison.Ordinal)
            && string.Equals(prefix, "fal-ai", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return prefix.ToLowerInvariant();
    }

    public static string? LabelOf(string name)
    {
        var separator = name.IndexOf(": ", StringComparison.Ordinal);
        if (separator < 0)
        {
            return null;
        }

        var label = name[..separator].Trim();
        return label.Length == 0 ? null : label;
    }

    public static IReadOnlyList<VendorGroup<T>> Group<T>(
        IEnumerable<T> items,
        Func<T, string> providerId,
        Func<T, string> modelId,
        Func<T, string> name)
    {
        var itemList = items.ToList();
        var identified = itemList
            .Select(item => (Item: item, Prefix: PrefixOf(providerId(item), modelId(item))))
            .Where(entry => entry.Prefix is not null)
            .GroupBy(entry => entry.Prefix!, StringComparer.Ordinal)
            .Select(group =>
            {
                var labels = group.Select(entry => LabelOf(name(entry.Item))).OfType<string>().ToList();
                var label = labels.Count == 0
                    ? group.Key
                    : labels.GroupBy(candidate => candidate, StringComparer.OrdinalIgnoreCase)
                        .OrderByDescending(candidates => candidates.Count())
                        .ThenBy(candidates => candidates.Key, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(candidates => candidates.Key, StringComparer.Ordinal)
                        .First().Key;
                return (Label: label, Items: group.Select(entry => entry.Item).ToList());
            })
            .ToList();

        var buckets = identified
            .GroupBy(group => group.Label, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Label = group.Select(item => item.Label).OrderBy(label => label, StringComparer.Ordinal).First(),
                Items = group.SelectMany(item => item.Items).ToList(),
            })
            .Select(group => new VendorGroup<T>(
                group.Label.ToLowerInvariant(),
                group.Label,
                Sort(group.Items, providerId, name)))
            .ToList();

        var unknown = itemList.Where(item => PrefixOf(providerId(item), modelId(item)) is null).ToList();
        if (unknown.Count > 0)
        {
            buckets.Add(new VendorGroup<T>(OtherKey, AppResources.ModelGroupOther, Sort(unknown, providerId, name)));
        }

        return buckets
            .OrderBy(group => group.Key == OtherKey)
            .ThenBy(group => group.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<T> Sort<T>(IEnumerable<T> items, Func<T, string> providerId, Func<T, string> name) =>
        items.OrderBy(name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(providerId, StringComparer.Ordinal)
            .ToList();
}
