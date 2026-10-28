namespace ImageGen.Core.Domain.Sessions;

/// <summary>
/// Parameters the user asks for, shared by all models of a session (F4.2). A null value means "the model's default":
/// that parameter is not sent. The names match the provider catalog; values are checked when they are saved.
/// </summary>
public sealed record GenerationParameters
{
    public static readonly GenerationParameters Empty = new();

    /// <summary>"W:H" with positive numbers, or "auto".</summary>
    public string? AspectRatio { get; init; }

    /// <summary>Free text up to 16 characters, for example "1K" or "1024".</summary>
    public string? Resolution { get; init; }

    /// <summary>One of png, jpeg, webp.</summary>
    public string? OutputFormat { get; init; }

    /// <summary>Free text up to 32 characters, for example "high".</summary>
    public string? Quality { get; init; }
}
