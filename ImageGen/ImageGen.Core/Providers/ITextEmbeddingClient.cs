namespace ImageGen.Core.Providers;

public interface ITextEmbeddingClient
{
    Task<EmbeddingResult> EmbedAsync(string modelId, IReadOnlyList<string> texts, CancellationToken cancellationToken);
}
