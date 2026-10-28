namespace ImageGen.Core.Providers;

/// <summary>
/// Image model listed in the config file. Used by providers whose API does not describe its models
/// (OpenAI-compatible); the id is sent to the API as is.
/// </summary>
public sealed record ConfiguredImageModel(string Id, string Name, bool SupportsSeed, int MaxImagesPerRequest);
