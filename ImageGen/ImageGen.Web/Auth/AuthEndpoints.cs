using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace ImageGen.Web.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints, string password)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var group = endpoints.MapGroup("/api/auth");
        group.MapPost("/login", async (LoginRequest request, HttpContext context, CancellationToken cancellationToken, TimeProvider timeProvider) =>
        {
            var expected = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            var actual = SHA256.HashData(Encoding.UTF8.GetBytes(request.Password ?? string.Empty));
            if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            {
                return Results.Json(new { error = "invalid_credentials" }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "owner")], CookieAuthenticationDefaults.AuthenticationScheme);
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), new AuthenticationProperties
            {
                IsPersistent = true,
                AllowRefresh = true,
                ExpiresUtc = timeProvider.GetUtcNow().AddDays(30),
            });
            return Results.Ok(new { authenticated = true });
        }).AllowAnonymous().RequireRateLimiting("login");

        group.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new { token = tokens.RequestToken });
        }).RequireAuthorization();

        group.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new { authenticated = true, user = user.Identity?.Name }))
            .RequireAuthorization();

        group.MapPost("/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Ok(new { authenticated = false });
        }).RequireAuthorization().AddEndpointFilter<AntiforgeryFilter>();

        return endpoints;
    }

    public sealed record LoginRequest(string? Password);

    public sealed class AntiforgeryFilter(IAntiforgery antiforgery) : IEndpointFilter
    {
        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context.HttpContext);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.BadRequest(new { error = "antiforgery_validation_failed" });
            }

            return await next(context);
        }
    }
}
