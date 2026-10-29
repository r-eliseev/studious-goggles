namespace ImageGen.Core.Search;

/// <summary>Where a searchable text comes from. The kind sets its weight in the ranking.</summary>
public enum SearchTextKind
{
    Title,
    Prompt,
    Description,
}
