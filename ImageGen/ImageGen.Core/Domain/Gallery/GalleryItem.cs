using ImageGen.Core.Domain.Models;

namespace ImageGen.Core.Domain.Gallery;

public sealed record GalleryItem(
    Guid GenerationId,
    Guid RunId,
    Guid SessionId,
    string SessionTitle,
    ModelRef Model,
    string Prompt,
    string ImagePath,
    string? MediaType,
    decimal? CostUsd,
    DateTimeOffset CompletedAt);
