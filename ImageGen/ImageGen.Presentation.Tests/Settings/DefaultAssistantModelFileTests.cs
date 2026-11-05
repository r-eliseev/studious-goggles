using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Presentation.Settings;
using ImageGen.Presentation.Tests.TestDoubles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

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
    public void Constructor_UnknownProviderInFile_UsesBuiltInAndLogsWithoutFileContents()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, DefaultAssistantModelFile.FileName), "\"gone:private-model\"");
        var logger = new CapturingLogger<DefaultAssistantModelFile>();

        var file = Create(logger);

        Assert.Equal(new ModelRef(ProviderIds.OpenRouter, SessionSettings.DefaultAssistantModelId), file.Current);
        var warning = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.DoesNotContain("gone", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("private-model", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_NonChatProviderInFile_UsesBuiltIn()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, DefaultAssistantModelFile.FileName), "\"fal:model\"");

        var file = Create();

        Assert.Equal(new ModelRef(ProviderIds.OpenRouter, SessionSettings.DefaultAssistantModelId), file.Current);
    }

    [Fact]
    public void Constructor_KnownChatProviderInFile_UsesConfiguredModel()
    {
        Directory.CreateDirectory(directory);
        var model = new ModelRef("my-llm", "m");
        File.WriteAllText(Path.Combine(directory, DefaultAssistantModelFile.FileName), "\"my-llm:m\"");

        var file = Create();

        Assert.Equal(model, file.Current);
    }

    [Fact]
    public async Task SaveAsync_Model_PersistsAndUpdatesCurrent()
    {
        var file = Create();
        var model = new ModelRef("my-llm", "vendor/assistant");

        await file.SaveAsync(model, TestContext.Current.CancellationToken);

        Assert.Equal(model, file.Current);
        Assert.Equal(model, new DefaultAssistantModelFile(new TestPaths(directory), CreateRouter(), NullLogger<DefaultAssistantModelFile>.Instance).Current);
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Fact]
    public async Task SaveAsync_UnknownProvider_ThrowsAndKeepsFileAndCurrentUnchanged()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, DefaultAssistantModelFile.FileName);
        var original = "\"openrouter:existing\"";
        await File.WriteAllTextAsync(path, original, TestContext.Current.CancellationToken);
        var file = Create();

        await Assert.ThrowsAsync<ArgumentException>(() => file.SaveAsync(new ModelRef("gone", "m"), TestContext.Current.CancellationToken));

        Assert.Equal(new ModelRef(ProviderIds.OpenRouter, "existing"), file.Current);
        Assert.Equal(original, await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    private DefaultAssistantModelFile Create() => Create(NullLogger<DefaultAssistantModelFile>.Instance);

    private DefaultAssistantModelFile Create(ILogger<DefaultAssistantModelFile> logger) =>
        new(new TestPaths(directory), CreateRouter(), logger);

    private static IProviderRouter CreateRouter()
    {
        var router = Substitute.For<IProviderRouter>();
        router.ChatProviderIds.Returns([ProviderIds.OpenRouter, "my-llm"]);
        return router;
    }

    private sealed record TestPaths(string DataDirectory) : IAppPaths;
}
