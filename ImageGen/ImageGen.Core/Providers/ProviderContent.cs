namespace ImageGen.Core.Providers;

/// <summary>A response stream that retains ownership of its HTTP response until disposed.</summary>
public sealed class ProviderContent : IAsyncDisposable
{
    private readonly HttpResponseMessage _response;
    private readonly CancellationTokenSource _timeout;

    public ProviderContent(Stream content, string mediaType, HttpResponseMessage response, CancellationTokenSource timeout)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(timeout);
        Content = content;
        MediaType = mediaType;
        _response = response;
        _timeout = timeout;
    }

    public Stream Content { get; }

    public string MediaType { get; }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Content.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _response.Dispose();
            _timeout.Dispose();
        }
    }
}
