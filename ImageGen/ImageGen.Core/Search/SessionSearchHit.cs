namespace ImageGen.Core.Search;

/// <summary>The best match of one session: its score, the kind of text it came from and a fragment of that text.</summary>
public sealed record SessionSearchHit(Guid SessionId, int Score, SearchTextKind Kind, string Snippet);
