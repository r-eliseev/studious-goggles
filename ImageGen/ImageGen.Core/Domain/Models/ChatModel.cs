namespace ImageGen.Core.Domain.Models;

/// <summary>A chat model as its provider lists it. Only what the app needs now; the rest (price, tools) comes later.</summary>
/// <param name="SupportsImageInput">True when the provider lists image among the input modalities.</param>
/// <param name="ContextLength">Context window in tokens, when the provider says so.</param>
public sealed record ChatModel(string Id, string Name, bool SupportsImageInput, int? ContextLength);
