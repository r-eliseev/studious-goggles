using System.Globalization;
using System.Text.RegularExpressions;

namespace ImageGen.Data;

/// <summary>
/// Ordered schema migrations. Every embedded <c>Migrations/NNNN_name.sql</c> file is one migration;
/// a new migration is a new file, without code changes. Never edit a released migration; append a new one.
/// </summary>
internal static class Migrations
{
    /// <summary>Prefix of the embedded resource names of migration scripts (see <c>ImageGen.Data.csproj</c>).</summary>
    public const string ResourcePrefix = "Migrations.";

    private static readonly Regex NamePattern = new("^[0-9]{4}_[a-z0-9_]+\\.sql$", RegexOptions.CultureInvariant);

    private static readonly Lazy<IReadOnlyList<Migration>> LazyAll = new(() => Load(ReadEmbeddedScripts()));

    /// <summary>All migrations in order. Throws <see cref="InvalidOperationException"/> if the set is invalid.</summary>
    public static IReadOnlyList<Migration> All => LazyAll.Value;

    /// <summary>
    /// Validates a set of named scripts and returns them ordered by number.
    /// Numbers must be 0001, 0002, … without gaps or repeats; names must match <c>NNNN_name.sql</c>.
    /// </summary>
    internal static IReadOnlyList<Migration> Load(IEnumerable<(string Name, string Script)> scripts)
    {
        ArgumentNullException.ThrowIfNull(scripts);

        var parsed = new List<Migration>();
        foreach (var (name, script) in scripts)
        {
            if (!NamePattern.IsMatch(name))
            {
                throw new InvalidOperationException(
                    $"Invalid migration name {name}: expected NNNN_name.sql (4 digits, lowercase Latin letters, digits, and _).");
            }

            var number = int.Parse(name.AsSpan(0, 4), CultureInfo.InvariantCulture);
            if (number == 0)
            {
                throw new InvalidOperationException(
                    $"Invalid migration number in {name}: numbering starts at 0001.");
            }

            parsed.Add(new Migration(number, name, script));
        }

        if (parsed.Count == 0)
        {
            throw new InvalidOperationException("There are no database schema migrations.");
        }

        parsed.Sort((left, right) => left.Number != right.Number
            ? left.Number.CompareTo(right.Number)
            : string.CompareOrdinal(left.Name, right.Name));

        for (var index = 0; index < parsed.Count; index++)
        {
            var expected = index + 1;
            var current = parsed[index];

            if (current.Number == expected)
            {
                continue;
            }

            if (current.Number < expected)
            {
                var previous = parsed[index - 1];
                throw new InvalidOperationException(
                    $"Two migration files have number {current.Number:D4}: {previous.Name}, {current.Name}. Renumber one of them.");
            }

            throw new InvalidOperationException($"Migration {expected:D4} is missing: the next file is {current.Name}.");
        }

        return parsed;
    }

    private static IEnumerable<(string Name, string Script)> ReadEmbeddedScripts()
    {
        var assembly = typeof(Migrations).Assembly;
        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Migration script '{resourceName}' is not readable.");
            using var reader = new StreamReader(stream);
            yield return (resourceName[ResourcePrefix.Length..], reader.ReadToEnd());
        }
    }
}
