namespace ImageGen.Core.Domain.Descriptions;

/// <summary>A text the assistant model wrote about one picture (task 019). The user decides whether it goes into the input field.</summary>
public sealed class ImageDescription
{
    public required Guid Id { get; set; }

    public required Guid SessionId { get; set; }

    /// <summary>The generation the picture comes from.</summary>
    public required Guid SourceGenerationId { get; set; }

    public required DescriptionMode Mode { get; set; }

    /// <summary>Provider of the model that wrote the text (<see cref="ImageGen.Core.Providers.ProviderIds"/>).</summary>
    public required string ProviderId { get; set; }

    public required string ModelId { get; set; }

    public required string Text { get; set; }

    /// <summary>Price of the request, or null when the provider did not say. Null is not zero.</summary>
    public decimal? CostUsd { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }
}
