namespace ImageGen.Core.Providers;

/// <summary>
/// What a provider needs to follow one queued job: the provider id and the request id with its addresses, taken from the
/// submit response. Stored with the generation (JSON), so a job can be followed again after a restart.
/// </summary>
public sealed record ProviderJobHandle(string ProviderId, string RequestId, string StatusUrl, string ResponseUrl, string CancelUrl);
