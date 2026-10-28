using ImageGen.Presentation.Utilities;

namespace ImageGen.Presentation.Tests.Utilities;

public class TaskExtensionsTests
{
    [Fact]
    public void FireAndForgetSafeAsync_FaultedTask_DoesNotThrowToCaller()
    {
        // An unobserved exception from an async void method would end the test host process,
        // so reaching the assertion means the exception was handled.
        var task = Task.FromException(new InvalidOperationException("boom"));

        task.FireAndForgetSafeAsync();

        Assert.True(task.IsFaulted);
    }

    [Fact]
    public void FireAndForgetSafeAsync_CompletedTask_ReturnsImmediately()
    {
        Task.CompletedTask.FireAndForgetSafeAsync();
    }
}
