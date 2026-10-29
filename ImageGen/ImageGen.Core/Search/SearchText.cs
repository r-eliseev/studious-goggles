namespace ImageGen.Core.Search;

/// <summary>One text of a session that the search looks into: the title, a prompt or a picture description.</summary>
public sealed record SearchText(Guid SessionId, SearchTextKind Kind, string Text);
