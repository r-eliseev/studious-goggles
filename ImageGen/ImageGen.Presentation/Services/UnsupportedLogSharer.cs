namespace ImageGen.Presentation.Services;

/// <summary>Share service used on shells without a supported system share sheet.</summary>
public sealed class UnsupportedLogSharer : ILogSharer
{
    public bool IsSupported => false;

    public Task ShareAsync(IReadOnlyList<string> files, CancellationToken cancellationToken) => Task.CompletedTask;
}
