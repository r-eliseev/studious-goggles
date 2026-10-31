namespace ImageGen.Core.Providers;

/// <summary>Chat model capabilities declared for an OpenAI-compatible or Anthropic-compatible provider.</summary>
public sealed record ConfiguredChatModel(string Id, string Name, bool SupportsImageInput, int? ContextLength);
