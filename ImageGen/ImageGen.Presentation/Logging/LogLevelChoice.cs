using Microsoft.Extensions.Logging;

namespace ImageGen.Presentation.Logging;

/// <summary>The level the app logs at, where it came from, and a warning when a setting was ignored.</summary>
public sealed record LogLevelChoice(LogLevel Level, string Source, string? Warning);
