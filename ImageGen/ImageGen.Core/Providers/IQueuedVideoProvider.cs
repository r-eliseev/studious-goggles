namespace ImageGen.Core.Providers;

/// <summary>Submits asynchronous video jobs, reads their status and opens their result stream.</summary>
public interface IQueuedVideoProvider
{
    Task<ProviderJobHandle> SubmitAsync(VideoGenerationRequest request, CancellationToken cancellationToken);

    Task<VideoJobUpdate> PollAsync(ProviderJobHandle handle, CancellationToken cancellationToken);

    Task<ProviderContent> OpenContentAsync(ProviderJobHandle handle, CancellationToken cancellationToken);
}
