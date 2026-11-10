namespace ImageGen.Core.Domain.Sessions;

/// <summary>Parameters shared by each audio model in a run.</summary>
public sealed record AudioParameters
{
    public static AudioParameters Default { get; } = new();

    public string? Voice { get; init; }
}
