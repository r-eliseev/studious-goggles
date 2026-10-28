using ImageGen.Core.Platform;
using ImageGen.Presentation.Appearance;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Presentation.Tests.Appearance;

public sealed class AppearanceServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "imagegen-tests", Guid.NewGuid().ToString("N"));

    public AppearanceServiceTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Current_NoFile_ReturnsDefault()
    {
        var service = CreateService();

        Assert.Equal(AppearanceSettings.Default, service.Current);
    }

    [Fact]
    public async Task SaveAsync_NewValue_WritesFileAndRaisesChanged()
    {
        var service = CreateService();
        var raised = new List<AppearanceSettings>();
        service.Changed += raised.Add;
        var gallery = new AppearanceSettings(AppearanceStyle.Gallery, AppearanceTheme.System);

        await service.SaveAsync(gallery, CancellationToken.None);

        Assert.Equal(gallery, service.Current);
        Assert.Contains("\"gallery\"", File.ReadAllText(Path.Combine(_directory, AppearanceSettingsFile.FileName)));
        Assert.Equal([gallery], raised);
    }

    [Fact]
    public async Task SaveAsync_SameValue_DoesNotRaiseChanged()
    {
        var service = CreateService();
        var raised = 0;
        service.Changed += _ => raised++;

        await service.SaveAsync(AppearanceSettings.Default, CancellationToken.None);

        Assert.Equal(0, raised);
    }

    [Fact]
    public async Task Current_AfterSaveInAnotherInstance_ReadsSavedValue()
    {
        var notebookDark = new AppearanceSettings(AppearanceStyle.Notebook, AppearanceTheme.Dark);
        await CreateService().SaveAsync(notebookDark, CancellationToken.None);

        var reader = CreateService();

        Assert.Equal(notebookDark, reader.Current);
    }

    private AppearanceService CreateService() => new(new TestAppPaths(_directory), NullLogger<AppearanceService>.Instance);

    private sealed record TestAppPaths(string DataDirectory) : IAppPaths;
}
