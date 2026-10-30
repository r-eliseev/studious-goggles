namespace ImageGen.Core.Context;

/// <summary>
/// The budget of one assistant request (C1.1, C1.6): the model's context minus the reserve for the answer and the
/// corrected fixed part, pictures and summary. The correction factor of the model applies to every estimated part
/// except the reserve, so that the request is counted the way the model counts it.
/// </summary>
public sealed record AssistantRequestBudget(
    int ContextLength,
    int FixedTokens,
    int ImageTokens,
    int SummaryTokens,
    double Factor)
{
    /// <summary>Tokens left for the history (C1.1). Never negative.</summary>
    public int HistoryBudget => Math.Max(0, ContextLength - TokenEstimator.ReserveForOutput - CorrectedFixedCost);

    /// <summary>The whole request as the model counts it: the reserve, the corrected fixed part and the corrected messages.</summary>
    public int CorrectedEstimate(IReadOnlyList<ContextMessage> selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        var messages = selected.Sum(message => (int)Math.Ceiling(TokenEstimator.Estimate(message.Text) * Factor));
        return TokenEstimator.ReserveForOutput + CorrectedFixedCost + messages;
    }

    /// <summary>The fixed part of the request with the correction applied: the fixed texts, the pictures and the summary.</summary>
    private int CorrectedFixedCost => (int)Math.Ceiling(Factor * (FixedTokens + ImageTokens + SummaryTokens));
}
