using System.Globalization;
using System.Text.RegularExpressions;

namespace ImageGen.Data;

/// <summary>
/// Ordered schema migrations. Every embedded <c>Migrations/NNNN_имя.sql</c> file is one migration;
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
    /// Numbers must be 0001, 0002, … without gaps or repeats; names must match <c>NNNN_имя.sql</c>.
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
                    $"Неверное имя миграции {name}: ожидается NNNN_имя.sql (4 цифры, строчные латинские буквы, цифры и _).");
            }

            var number = int.Parse(name.AsSpan(0, 4), CultureInfo.InvariantCulture);
            parsed.Add(new Migration(number, name, script));
        }

        if (parsed.Count == 0)
        {
            throw new InvalidOperationException("Нет ни одной миграции схемы базы данных.");
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
                    $"Два файла миграции с номером {current.Number:D4}: {previous.Name}, {current.Name}. Перенумеруйте один из них.");
            }

            throw new InvalidOperationException($"Пропущена миграция {expected:D4}: следующий файл — {current.Name}.");
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
