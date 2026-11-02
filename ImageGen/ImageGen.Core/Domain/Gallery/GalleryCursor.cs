namespace ImageGen.Core.Domain.Gallery;

public sealed record GalleryCursor(DateTimeOffset CompletedAt, Guid GenerationId);
