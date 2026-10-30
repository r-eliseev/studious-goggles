using System.Globalization;
using System.Net;
using System.Net.Sockets;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Providers;

namespace ImageGen.Core.Fal;

internal static class FalUrlValidator
{
    private const string InvalidProviderUrlMessage = "fal returned an invalid URL or model endpoint.";

    public static Uri ValidateCallback(string value, Uri baseAddress)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !HasSafeAbsoluteSyntax(value, uri)
            || (string.Equals(baseAddress.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(baseAddress.IdnHost, "localhost", StringComparison.OrdinalIgnoreCase))
            || HasAuthorityUserInfo(value)
            || uri.UserInfo.Length != 0
            || uri.Fragment.Length != 0
            || !string.Equals(uri.Scheme, baseAddress.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(uri.IdnHost, baseAddress.IdnHost, StringComparison.OrdinalIgnoreCase)
            || uri.Port != baseAddress.Port)
        {
            throw InvalidProviderUrl();
        }

        return uri;
    }

    public static Uri ValidateImage(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !HasSafeAbsoluteSyntax(value, uri)
            || HasAuthorityUserInfo(value)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || uri.UserInfo.Length != 0
            || uri.Fragment.Length != 0
            || IsNonPublicHost(uri))
        {
            throw InvalidProviderUrl();
        }

        return uri;
    }

    public static string ValidateModelEndpoint(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value[0] == '/'
            || value.Contains('\\', StringComparison.Ordinal)
            || value.Contains('?', StringComparison.Ordinal)
            || value.Contains('#', StringComparison.Ordinal)
            || value.Any(char.IsWhiteSpace)
            || value.Any(char.IsControl)
            || Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            throw InvalidProviderUrl();
        }

        foreach (var segment in value.Split('/'))
        {
            if (segment.Length == 0 || HasInvalidPercentEncoding(segment))
            {
                throw InvalidProviderUrl();
            }

            var decoded = Uri.UnescapeDataString(segment);
            if (decoded is "." or ".."
                || decoded.Contains('/', StringComparison.Ordinal)
                || decoded.Contains('\\', StringComparison.Ordinal)
                || decoded.Contains('%', StringComparison.Ordinal))
            {
                throw InvalidProviderUrl();
            }
        }

        return value;
    }

    private static bool HasInvalidPercentEncoding(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '%')
            {
                continue;
            }

            if (index + 2 >= value.Length
                || !byte.TryParse(value.AsSpan(index + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
            {
                return true;
            }

            index += 2;
        }

        return false;
    }

    private static bool HasSafeAbsoluteSyntax(string value, Uri uri) =>
        value.StartsWith(uri.Scheme + "://", StringComparison.OrdinalIgnoreCase)
        && !value.Contains('\\', StringComparison.Ordinal)
        && !value.Contains('#', StringComparison.Ordinal);

    private static bool HasAuthorityUserInfo(string value)
    {
        var authorityStart = value.IndexOf("://", StringComparison.Ordinal);
        if (authorityStart < 0)
        {
            return true;
        }

        authorityStart += 3;
        var authorityEnd = value.IndexOfAny(['/', '?', '#'], authorityStart);
        if (authorityEnd < 0)
        {
            authorityEnd = value.Length;
        }

        return value.AsSpan(authorityStart, authorityEnd - authorityStart).Contains('@');
    }

    private static bool IsNonPublicHost(Uri uri)
    {
        var host = uri.Host.TrimEnd('.');
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var unbracketedHost = host.Trim('[', ']');
        if (unbracketedHost.Contains('%', StringComparison.Ordinal))
        {
            return true;
        }

        if (!IPAddress.TryParse(unbracketedHost, out var address))
        {
            return false;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.Equals(IPAddress.IPv6Any)
                || address.Equals(IPAddress.IPv6Loopback)
                || address.IsIPv6LinkLocal
                || address.IsIPv6Multicast
                || (address.GetAddressBytes()[0] & 0xfe) == 0xfc;
        }

        var bytes = address.GetAddressBytes();
        return bytes[0] == 0
            || bytes[0] == 10
            || bytes[0] == 127
            || (bytes[0] == 169 && bytes[1] == 254)
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            || (bytes[0] == 192 && bytes[1] == 168)
            || (bytes[0] & 0xf0) == 0xe0;
    }

    private static ProviderException InvalidProviderUrl() => new(ErrorKind.Unknown, InvalidProviderUrlMessage);
}
