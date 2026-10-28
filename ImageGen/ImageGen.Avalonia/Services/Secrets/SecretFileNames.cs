using System.Text.RegularExpressions;

namespace ImageGen.Avalonia.Services.Secrets;

/// <summary>File layout of secrets kept as files in the data folder (DPAPI on Windows).</summary>
public static class SecretFileNames
{
    private static readonly Regex SafeName = new("^[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant);

    /// <summary>Path of the file of a secret: <c>&lt;dataDirectory&gt;/secrets/&lt;name&gt;.bin</c>. The name must be a plain file name.</summary>
    public static string PathFor(string dataDirectory, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!SafeName.IsMatch(name))
        {
            throw new ArgumentException("Недопустимое имя секрета: допустимы латинские буквы, цифры, «_», «-» и «.».", nameof(name));
        }

        return Path.Combine(dataDirectory, "secrets", name + ".bin");
    }
}
