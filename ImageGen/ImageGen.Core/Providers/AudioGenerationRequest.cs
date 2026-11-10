using ImageGen.Core.Domain.Models;

namespace ImageGen.Core.Providers;

public sealed record AudioGenerationRequest(AudioModel Model, string Prompt, string? Voice);
