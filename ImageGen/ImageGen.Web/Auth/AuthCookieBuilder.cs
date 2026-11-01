using Microsoft.AspNetCore.Http;

namespace ImageGen.Web.Auth;

public sealed class AuthCookieBuilder : CookieBuilder
{
    public override CookieOptions Build(HttpContext context, DateTimeOffset expiresFrom)
    {
        var options = base.Build(context, expiresFrom);
        var localHost = string.Equals(context.Request.Host.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || System.Net.IPAddress.TryParse(context.Request.Host.Host, out var address) && System.Net.IPAddress.IsLoopback(address);
        options.Secure = !localHost;
        return options;
    }
}
