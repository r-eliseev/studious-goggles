namespace ImageGen.Core.Providers;

/// <summary>
/// A provider that runs generations as queued jobs (fal): submit returns at once with a handle, the state is read by
/// polling, and a job can be cancelled. Errors are thrown as <see cref="ProviderException"/>.
/// </summary>
public interface IQueuedImageProvider
{
    Task<ProviderJobHandle> SubmitAsync(ImageGenerationRequest request, CancellationToken cancellationToken);

    /// <summary>Reads the state once.</summary>
    Task<JobUpdate> PollAsync(ProviderJobHandle handle, CancellationToken cancellationToken);

    Task<CancelOutcome> CancelAsync(ProviderJobHandle handle, CancellationToken cancellationToken);
}
