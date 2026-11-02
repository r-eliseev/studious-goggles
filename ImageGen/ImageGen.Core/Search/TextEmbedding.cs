namespace ImageGen.Core.Search;

/// <summary>A locally stored vector for one version of a searchable text.</summary>
public sealed record TextEmbedding(
    string SourceKey,
    Guid SessionId,
    SearchTextKind Kind,
    string ContentHash,
    string Model,
    int Dimensions,
    float[] Vector,
    DateTimeOffset CreatedAt);
