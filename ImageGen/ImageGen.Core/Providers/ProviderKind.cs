namespace ImageGen.Core.Providers;

/// <summary>Wire format of a provider. The config file names the kind in camelCase (<c>openRouter</c>).</summary>
public enum ProviderKind
{
    /// <summary>OpenRouter: images through <c>images</c>, chat through <c>chat/completions</c>.</summary>
    OpenRouter = 0,

    /// <summary>Any OpenAI-compatible API: images through <c>images/generations</c>, chat through <c>chat/completions</c>.</summary>
    OpenAiCompatible = 1,

    /// <summary>Anthropic-compatible Messages API: assistant chat only, no images.</summary>
    AnthropicCompatible = 2,
}
