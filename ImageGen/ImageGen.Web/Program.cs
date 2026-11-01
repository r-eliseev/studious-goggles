using ImageGen.Core;
using ImageGen.Core.Platform;
using ImageGen.Data;
using ImageGen.Web;
using ImageGen.Web.Secrets;
using Microsoft.AspNetCore.DataProtection;

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
builder.Services.AddSingleton<ISecretStore, DataProtectionSecretStore>();
builder.Services.AddImageGenCore();
builder.Services.AddImageGenData();
builder.Services.AddHostedService<StartupInitializer>();

var app = builder.Build();
app.MapGet("/api/health", () => Results.Json(new { status = "ok" }));
app.Run();

public partial class Program;
