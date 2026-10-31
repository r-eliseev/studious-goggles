namespace ImageGen.Core.Providers;

/// <summary>Decodes a base64 payload while applying a decoded-byte limit without copying the encoded text.</summary>
internal static class BoundedBase64ImageDecoder
{
    public static byte[] Decode(string base64, long limitBytes)
    {
        ArgumentNullException.ThrowIfNull(base64);
        ArgumentOutOfRangeException.ThrowIfNegative(limitBytes);

        long encodedCharacters = 0;
        foreach (var character in base64)
        {
            if (!IsBase64Whitespace(character)) encodedCharacters++;
        }

        var maximumEncodedLength = checked(((limitBytes + 2) / 3) * 4);
        if (encodedCharacters > maximumEncodedLength)
        {
            throw new ProviderResponseTooLargeException(limitBytes);
        }

        var bytes = Convert.FromBase64String(base64);
        if (bytes.LongLength > limitBytes)
        {
            throw new ProviderResponseTooLargeException(limitBytes);
        }

        return bytes;
    }

    private static bool IsBase64Whitespace(char value) => value is ' ' or '\t' or '\r' or '\n';
}
