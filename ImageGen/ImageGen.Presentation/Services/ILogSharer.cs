namespace ImageGen.Presentation.Services;

/// <summary>Opens the system share sheet for a collection of files.</summary>
public interface ILogSharer
{
    bool IsSupported { get; }

    Task ShareAsync(IReadOnlyList<string> files, CancellationToken cancellationToken);
}
