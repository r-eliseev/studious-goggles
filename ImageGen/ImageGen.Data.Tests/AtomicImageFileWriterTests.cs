namespace ImageGen.Data.Tests;

public sealed class AtomicImageFileWriterTests
{
    [Fact]
    public async Task WriteAsync_Succeeds_PublishesBytesAndRemovesTemporaryFile()
    {
        var directory = CreateDirectory();
        try
        {
            var final = Path.Combine(directory, "image.png");
            var temporary = Path.Combine(directory, "image.tmp");
            var writer = new AtomicImageFileWriter(_ => temporary);

            await writer.WriteAsync(final, new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);

            Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(final, TestContext.Current.CancellationToken));
            Assert.False(File.Exists(temporary));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task WriteAsync_PartialWriteFails_PreservesExistingFinalAndCleansTemporaryFile()
    {
        var directory = CreateDirectory();
        try
        {
            var final = Path.Combine(directory, "image.png");
            var temporary = Path.Combine(directory, "image.tmp");
            await File.WriteAllBytesAsync(final, [9, 9], TestContext.Current.CancellationToken);
            var writer = new AtomicImageFileWriter(_ => temporary, async (stream, content, cancellationToken) =>
            {
                await stream.WriteAsync(content[..1], cancellationToken);
                throw new IOException("expected write failure");
            });

            await Assert.ThrowsAsync<IOException>(() => writer.WriteAsync(final, new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken));

            Assert.Equal(new byte[] { 9, 9 }, await File.ReadAllBytesAsync(final, TestContext.Current.CancellationToken));
            Assert.False(File.Exists(temporary));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task WriteAsync_PreCancelled_DoesNotCreateTemporaryOrFinalFile()
    {
        var directory = CreateDirectory();
        try
        {
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();
            var final = Path.Combine(directory, "image.png");
            var writer = new AtomicImageFileWriter();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.WriteAsync(final, new byte[] { 1 }, cancellation.Token));

            Assert.False(File.Exists(final));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task WriteAsync_CancelledAfterWriteBeforeCommit_DoesNotPublishFinal()
    {
        var directory = CreateDirectory();
        try
        {
            using var cancellation = new CancellationTokenSource();
            var final = Path.Combine(directory, "image.png");
            var temporary = Path.Combine(directory, "image.tmp");
            var writer = new AtomicImageFileWriter(_ => temporary, (stream, content, token) =>
            {
                cancellation.Cancel();
                return Task.CompletedTask;
            });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.WriteAsync(final, new byte[] { 1 }, cancellation.Token));

            Assert.False(File.Exists(final));
            Assert.False(File.Exists(temporary));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task WriteAsync_CreateNewCollision_DoesNotDeleteFileItDoesNotOwn()
    {
        var directory = CreateDirectory();
        try
        {
            var final = Path.Combine(directory, "image.png");
            var temporary = Path.Combine(directory, "image.tmp");
            await File.WriteAllBytesAsync(temporary, [7, 7], TestContext.Current.CancellationToken);
            var writer = new AtomicImageFileWriter(_ => temporary);

            await Assert.ThrowsAsync<IOException>(() => writer.WriteAsync(final, new byte[] { 1 }, TestContext.Current.CancellationToken));

            Assert.Equal(new byte[] { 7, 7 }, await File.ReadAllBytesAsync(temporary, TestContext.Current.CancellationToken));
            Assert.False(File.Exists(final));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task WriteAsync_CleanupFails_PreservesOriginalWriteException()
    {
        var directory = CreateDirectory();
        try
        {
            var final = Path.Combine(directory, "image.png");
            var temporary = Path.Combine(directory, "image.tmp");
            var writer = new AtomicImageFileWriter(_ => temporary,
                (stream, content, _) => throw new InvalidDataException("original failure"),
                _ => throw new IOException("cleanup failure"));

            var error = await Assert.ThrowsAsync<InvalidDataException>(() => writer.WriteAsync(final, new byte[] { 1 }, TestContext.Current.CancellationToken));

            Assert.Equal("original failure", error.Message);
            Assert.False(File.Exists(final));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task WriteAsync_SuccessfulOverwrite_ReplacesPreviousFile()
    {
        var directory = CreateDirectory();
        try
        {
            var final = Path.Combine(directory, "image.png");
            await File.WriteAllBytesAsync(final, [8], TestContext.Current.CancellationToken);
            var writer = new AtomicImageFileWriter();

            await writer.WriteAsync(final, new byte[] { 1, 2 }, TestContext.Current.CancellationToken);

            Assert.Equal(new byte[] { 1, 2 }, await File.ReadAllBytesAsync(final, TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "imagegen-atomic-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
