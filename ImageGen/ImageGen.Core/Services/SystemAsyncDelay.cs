namespace ImageGen.Core.Services;

public sealed class SystemAsyncDelay(TimeProvider timeProvider) : IAsyncDelay
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, timeProvider, cancellationToken);
}
