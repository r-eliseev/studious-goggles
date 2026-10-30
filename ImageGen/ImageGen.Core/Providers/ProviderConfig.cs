namespace ImageGen.Core.Providers;

/// <summary>
/// One provider from the config file. Contains no keys: the key is stored in the secret store under
/// <c>SecretNames.ForProvider(Id)</c>.
/// </summary>
/// <param name="Id">Stable id: lowercase letters, digits and '-'. Also the prefix of model references.</param>
/// <param name="BaseAddress">Absolute address with a trailing slash; endpoints are relative to it.</param>
/// <param name="ImageModels">Models for <see cref="ProviderKind.OpenAiCompatible"/>; empty for other kinds.</param>
public sealed record ProviderConfig(
    string Id,
    ProviderKind Kind,
    string DisplayName,
    Uri BaseAddress,
    TimeSpan Timeout,
    IReadOnlyList<ConfiguredImageModel> ImageModels,
    string? ImageResponseFormat = null,
    string? ImageOutputFormat = null);
