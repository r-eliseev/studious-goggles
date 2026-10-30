namespace ImageGen.Core.Services;

public interface IAsyncDelay
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}
