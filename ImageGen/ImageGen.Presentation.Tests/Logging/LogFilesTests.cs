using ImageGen.Presentation.Logging;

namespace ImageGen.Presentation.Tests.Logging;

public sealed class LogFilesTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "imagegen-log-tests", Guid.NewGuid().ToString("N"));

    public LogFilesTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void List_ReturnsOnlyLogFilesInOrdinalOrder()
    {
        var paths = CreateLogs("imagegen-20261101.log", "imagegen-20261102.log", "imagegen-20261102_001.log", "notes.txt");

        var logs = LogFiles.List(_directory);

        Assert.Equal(paths.Take(3), logs);
    }

    [Fact]
    public void DeleteAllExceptNewest_RemovesOlderLogsAndKeepsNewest()
    {
        var paths = CreateLogs("imagegen-20261101.log", "imagegen-20261102.log", "imagegen-20261102_001.log", "notes.txt");

        var deleted = LogFiles.DeleteAllExceptNewest(_directory);

        Assert.Equal(2, deleted);
        Assert.False(File.Exists(paths[0]));
        Assert.False(File.Exists(paths[1]));
        Assert.True(File.Exists(paths[2]));
        Assert.True(File.Exists(paths[3]));
    }

    [Fact]
    public void List_AndDeleteAllExceptNewest_WhenDirectoryMissing_ReturnEmptyAndZero()
    {
        var missing = Path.Combine(_directory, "missing");

        Assert.Empty(LogFiles.List(missing));
        Assert.Equal(0, LogFiles.DeleteAllExceptNewest(missing));
    }

    [Fact]
    public async Task CopyForSharingAsync_CopiesFileOpenForAppend()
    {
        var source = Path.Combine(_directory, "imagegen-20261102.log");
        var target = Path.Combine(_directory, "share");
        await File.WriteAllTextAsync(source, "current log", TestContext.Current.CancellationToken);
        await using var openLog = new FileStream(source, FileMode.Append, FileAccess.Write, FileShare.Read);

        var copies = await LogFiles.CopyForSharingAsync(_directory, target, TestContext.Current.CancellationToken);

        var copy = Assert.Single(copies);
        Assert.Equal("imagegen-20261102.log", Path.GetFileName(copy));
        Assert.Equal("current log", await File.ReadAllTextAsync(copy, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CopyForSharingAsync_RemovesStaleCopyBeforeCopyingCurrentLogs()
    {
        var source = Path.Combine(_directory, "imagegen-20261102.log");
        var target = Path.Combine(_directory, "share");
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(source, "current", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(target, "imagegen-old.log"), "stale", TestContext.Current.CancellationToken);

        var copies = await LogFiles.CopyForSharingAsync(_directory, target, TestContext.Current.CancellationToken);

        Assert.Single(copies);
        Assert.False(File.Exists(Path.Combine(target, "imagegen-old.log")));
    }

    private string[] CreateLogs(params string[] names)
    {
        return names.Select(name =>
        {
            var path = Path.Combine(_directory, name);
            File.WriteAllText(path, name);
            return path;
        }).ToArray();
    }
}
