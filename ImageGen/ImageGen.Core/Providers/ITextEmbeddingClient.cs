namespace ImageGen.Core.Providers;

public interface ITextEmbeddingClient
{
    Task<EmbeddingResult> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken);
}
