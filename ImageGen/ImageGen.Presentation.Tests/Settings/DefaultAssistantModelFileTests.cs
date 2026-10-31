using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Platform;
using ImageGen.Presentation.Settings;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Presentation.Tests.Settings;

public sealed class DefaultAssistantModelFileTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "imagegen-default-model-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Constructor_InvalidFile_UsesBuiltInModel()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, DefaultAssistantModelFile.FileName), "not json");
        var file = Create();

        Assert.Equal(new ModelRef("openrouter", SessionSettings.DefaultAssistantModelId), file.Current);
    }

    [Fact]
    public async Task SaveAsync_Model_PersistsAndUpdatesCurrent()
    {
        var file = Create();
        var model = new ModelRef("claude", "vendor/assistant");

        await file.SaveAsync(model, TestContext.Current.CancellationToken);

        Assert.Equal(model, file.Current);
        Assert.Equal(model, new DefaultAssistantModelFile(new TestPaths(directory), NullLogger<DefaultAssistantModelFile>.Instance).Current);
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    private DefaultAssistantModelFile Create() => new(new TestPaths(directory), NullLogger<DefaultAssistantModelFile>.Instance);

    private sealed record TestPaths(string DataDirectory) : IAppPaths;
}
