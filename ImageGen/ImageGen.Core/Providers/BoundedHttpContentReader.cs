using System.Text;

namespace ImageGen.Core.Providers;

public static class BoundedHttpContentReader
{
    public static async Task<byte[]> ReadBytesAsync(HttpContent content, long limitBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegative(limitBytes);

        var declaredLength = content.Headers.ContentLength;
        if (declaredLength > limitBytes)
        {
            throw new ProviderResponseTooLargeException(limitBytes, declaredLength);
        }

        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var output = new MemoryStream((int)Math.Min(limitBytes, 81920));
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            var remainingWithProbe = limitBytes - total + 1;
            var requested = (int)Math.Min(buffer.Length, remainingWithProbe);
            var read = await stream.ReadAsync(buffer.AsMemory(0, requested), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return output.ToArray();
            }

            total += read;
            if (total > limitBytes)
            {
                throw new ProviderResponseTooLargeException(limitBytes, declaredLength);
            }

            output.Write(buffer, 0, read);
        }
    }

    public static async Task<string> ReadTextAsync(HttpContent content, long limitBytes, CancellationToken cancellationToken)
    {
        var bytes = await ReadBytesAsync(content, limitBytes, cancellationToken).ConfigureAwait(false);
        var encoding = Encoding.UTF8;
        var charset = content.Headers.ContentType?.CharSet;
        if (!string.IsNullOrWhiteSpace(charset))
        {
            try
            {
                encoding = Encoding.GetEncoding(charset.Trim('"'));
            }
            catch (ArgumentException)
            {
                // Match the default HTTP text encoding when the provider sends an unknown charset.
            }
        }

        return encoding.GetString(bytes);
    }
}
