namespace ImageGen.Core.Domain.Models;

/// <summary>A chat model as its provider lists it. Only what the app needs: input images, tools, price and context.</summary>
/// <param name="SupportsImageInput">True when the provider lists image among the input modalities.</param>
/// <param name="ContextLength">Context window in tokens, when the provider says so.</param>
/// <param name="SupportsTools">True when the provider lists tools among the supported parameters. The assistant needs them to start runs.</param>
/// <param name="PromptPricePerMillion">Price of one million input tokens in USD, when the provider says so.</param>
/// <param name="CompletionPricePerMillion">Price of one million output tokens in USD, when the provider says so.</param>
public sealed record ChatModel(
    string Id,
    string Name,
    bool SupportsImageInput,
    int? ContextLength,
    bool SupportsTools = false,
    decimal? PromptPricePerMillion = null,
    decimal? CompletionPricePerMillion = null)
{
    /// <summary>Free models have the <c>:free</c> suffix or zero prices. The provider may rate-limit them.</summary>
    public bool IsFree =>
        Id.EndsWith(":free", StringComparison.Ordinal)
        || (PromptPricePerMillion == 0 && CompletionPricePerMillion == 0);
}
