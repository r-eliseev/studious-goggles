namespace ImageGen.Core.Search;

public sealed record ReindexEstimate(int Sources, long ApproxTokens, decimal? CostUsd);
