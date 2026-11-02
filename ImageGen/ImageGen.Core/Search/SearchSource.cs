namespace ImageGen.Core.Search;

/// <summary>A searchable text with an identity stable enough to refresh its embedding independently.</summary>
public sealed record SearchSource(string SourceKey, Guid SessionId, SearchTextKind Kind, string Text);
