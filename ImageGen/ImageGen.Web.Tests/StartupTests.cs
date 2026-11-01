using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ImageGen.Web.Tests;

[Collection(WebTestCollection.Name)]
public sealed class StartupTests
{
    [Fact]
    public async Task Startup_HealthEndpointWorksAndDatabaseIsInitialized()
    {
        var directory = Path.Combine(Path.GetTempPath(), "imagegen-web-tests", Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("IMAGEGEN_DATA_DIR", directory);
        Environment.SetEnvironmentVariable("IMAGEGEN_WEB_PASSWORD", "test-password");
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));

            var client = factory.CreateClient();
            var response = await client.GetAsync("/api/health", TestContext.Current.CancellationToken);

            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("{\"status\":\"ok\"}", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.True(File.Exists(Path.Combine(directory, "imagegen.db")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("IMAGEGEN_DATA_DIR", null);
            Environment.SetEnvironmentVariable("IMAGEGEN_WEB_PASSWORD", null);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Startup_WithoutPasswordFailsClearly()
    {
        var directory = Path.Combine(Path.GetTempPath(), "imagegen-web-tests", Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("IMAGEGEN_DATA_DIR", directory);
        Environment.SetEnvironmentVariable("IMAGEGEN_WEB_PASSWORD", "");
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));

            var exception = await Assert.ThrowsAnyAsync<Exception>(() => factory.CreateClient().GetAsync("/api/health", TestContext.Current.CancellationToken));

            Assert.Contains("IMAGEGEN_WEB_PASSWORD", exception.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("IMAGEGEN_DATA_DIR", null);
            Environment.SetEnvironmentVariable("IMAGEGEN_WEB_PASSWORD", null);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
