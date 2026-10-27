using System.Diagnostics;

namespace ImageGen.Maui.Utilities;

public static class TaskExtensions
{
    /// <summary>
    /// Runs a task from a synchronous event handler (for example <c>OnAppearing</c>) without losing exceptions.
    /// This is the only allowed place for <c>async void</c>.
    /// </summary>
    public static async void FireAndForgetSafeAsync(this Task task)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unhandled background exception: {ex}");
        }
    }
}
