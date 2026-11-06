namespace ImageGen.Core.Domain.Sessions;

/// <summary>Parameters shared by each video model in a run.</summary>
public sealed record VideoParameters
{
    public static VideoParameters Default { get; } = new();

    public int DurationSeconds { get; init; } = 5;

    public string? Resolution { get; init; }

    public string? AspectRatio { get; init; }

    public bool GenerateAudio { get; init; } = true;
}
