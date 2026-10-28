using ImageGen.Core.Domain.Sessions;

namespace ImageGen.Core.Domain.Generations;

public sealed class Run
{
    public required Guid Id { get; set; }

    public required Guid SessionId { get; set; }

    public required string Prompt { get; set; }

    public required RunOrigin Origin { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }

    /// <summary>Parameters the session asked for when the run was created. Repeat, new seed and edit reuse them.</summary>
    public GenerationParameters Parameters { get; set; } = GenerationParameters.Empty;
}
