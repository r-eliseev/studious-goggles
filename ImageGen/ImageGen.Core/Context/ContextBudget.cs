namespace ImageGen.Core.Context;

/// <summary>
/// The part of the model's context left for the history (C1.1): the context minus the reserve for the answer and the
/// estimated fixed part, pictures and summary. Never negative.
/// </summary>
public sealed record ContextBudget(int ContextLength, int FixedTokens, int ImageTokens, int SummaryTokens)
{
    public int HistoryBudget =>
        Math.Max(0, ContextLength - TokenEstimator.ReserveForOutput - FixedTokens - ImageTokens - SummaryTokens);
}
