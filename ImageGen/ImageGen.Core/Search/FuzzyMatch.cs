namespace ImageGen.Core.Search;

/// <summary>A successful match of a query against one text: the sum of the token scores and the position of the first matched word.</summary>
public readonly record struct FuzzyMatch(int Score, int FirstIndex);
