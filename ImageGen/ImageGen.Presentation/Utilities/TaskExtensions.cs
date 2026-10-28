using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Presentation.Utilities;

public static class TaskExtensions
{
    /// <summary>Logger of the background failures. The shell sets it at start; until then they are dropped.</summary>
    public static ILogger Logger { get; set; } = NullLogger.Instance;

    /// <summary>
    /// Runs a task from a synchronous event handler (for example <c>OnAppearing</c>) without losing exceptions.
    /// A failure is logged as an error with its exception. This is the only allowed place for <c>async void</c>.
    /// </summary>
    public static async void FireAndForgetSafeAsync(this Task task)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unhandled background exception.");
        }
    }
}
