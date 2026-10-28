namespace ImageGen.Core.Context;

/// <summary>
/// Rough token counts for the assistant's history (ADR 0015). The estimate is corrected per model by
/// <see cref="TokenCorrection"/> after each reply.
/// </summary>
public static class TokenEstimator
{
    /// <summary>Tokens a picture takes in a request (the price in the spec is unknown; C1.1).</summary>
    public const int ImageTokens = 1600;

    /// <summary>Left for the answer (C1.1).</summary>
    public const int ReserveForOutput = 4096;

    /// <summary>Context length used when the model's context is not known (C1.1).</summary>
    public const int UnknownContextLength = 32000;

    private const double CharactersPerToken = 3.5;
    private const int MessageOverhead = 4;

    public static int Estimate(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return (int)Math.Ceiling(text.Length / CharactersPerToken) + MessageOverhead;
    }

    public static int EstimateImages(int count) => count * ImageTokens;
}
