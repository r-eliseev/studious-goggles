using ImageGen.Presentation.Tests.TestDoubles;
using ImageGen.Presentation.Utilities;
using Microsoft.Extensions.Logging;
using AppTaskExtensions = ImageGen.Presentation.Utilities.TaskExtensions;

namespace ImageGen.Presentation.Tests.Utilities;

public class TaskExtensionsTests
{
    [Fact]
    public async Task FireAndForgetSafeAsync_FaultedTask_LogsErrorWithException()
    {
        var logger = new CapturingLogger<TaskExtensionsTests>();
        var previous = AppTaskExtensions.Logger;
        AppTaskExtensions.Logger = logger;
        try
        {
            Task.FromException(new InvalidOperationException("boom")).FireAndForgetSafeAsync();

            var entry = await WaitForEntryAsync(logger, TestContext.Current.CancellationToken);
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.IsType<InvalidOperationException>(entry.Exception);
        }
        finally
        {
            AppTaskExtensions.Logger = previous;
        }
    }

    private static async Task<LogEntry> WaitForEntryAsync(CapturingLogger<TaskExtensionsTests> logger, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var entry = logger.Entries.FirstOrDefault();
            if (entry is not null)
            {
                return entry;
            }

            await Task.Delay(10, cancellationToken);
        }

        throw new TimeoutException("The faulted task was not logged.");
    }
}
