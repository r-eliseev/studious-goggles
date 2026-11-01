using System.Threading.RateLimiting;
using ImageGen.Core;
using ImageGen.Core.Platform;
using ImageGen.Data;
using ImageGen.Web;
using ImageGen.Web.Api;
using ImageGen.Web.Auth;
using ImageGen.Web.Events;
using ImageGen.Web.Secrets;
using ImageGen.Web.Settings;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var password = builder.Configuration["IMAGEGEN_WEB_PASSWORD"];
if (string.IsNullOrWhiteSpace(password))
{
    throw new InvalidOperationException("IMAGEGEN_WEB_PASSWORD must be set to a non-empty value.");
}

builder.Services.AddSingleton<IAppPaths, WebAppPaths>();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Path.GetFullPath(builder.Configuration["IMAGEGEN_DATA_DIR"] ?? "/data"), "keys")))
    .SetApplicationName("ImageGen");
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie = new AuthCookieBuilder
        {
            Name = "imagegen.auth",
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
        };
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return context.Response.WriteAsJsonAsync(new { error = "unauthorized" });
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return context.Response.WriteAsJsonAsync(new { error = "forbidden" });
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
    options.OnRejected = (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        return new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(new { error = "rate_limited" }, cancellationToken));
    };
});
builder.Services.AddSingleton<ISecretStore, DataProtectionSecretStore>();
builder.Services.AddSingleton<EventHub>();
builder.Services.AddSingleton<ImageGen.Core.Services.IDefaultAssistantModel, WebDefaultAssistantModel>();
builder.Services.AddImageGenCore();
builder.Services.AddImageGenData();
builder.Services.AddHostedService<StartupInitializer>();

var app = builder.Build();
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { error = "unknown" });
}));
app.MapGet("/api/health", () => Results.Json(new { status = "ok" })).AllowAnonymous();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapAuthEndpoints(password);
app.MapWebApiEndpoints();
app.MapEventsEndpoint();
app.Run();

public partial class Program;
