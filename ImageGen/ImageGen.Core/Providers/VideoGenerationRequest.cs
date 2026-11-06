namespace ImageGen.Core.Providers;

/// <summary>Normalized video generation inputs shared by video providers.</summary>
public sealed record VideoGenerationRequest(
    string ModelId,
    string Prompt,
    int DurationSeconds,
    string? Resolution,
    string? AspectRatio,
    bool GenerateAudio,
    long? Seed = null,
    ImageReference? FirstFrame = null);
