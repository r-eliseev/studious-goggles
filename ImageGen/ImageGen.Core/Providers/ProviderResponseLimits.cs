namespace ImageGen.Core.Providers;

public enum ProviderResponseBodyKind
{
    Json,
    GeneratedImageJson,
    Image,
}

public static class ProviderResponseLimits
{
    public const long Json = 8L * 1024 * 1024;
    public const long GeneratedImageJson = 72L * 1024 * 1024;
    public const long Image = 50L * 1024 * 1024;

    public static readonly HttpRequestOptionsKey<ProviderResponseBodyKind> BodyKind = new("ImageGen.ProviderResponseBodyKind");
    public static readonly HttpRequestOptionsKey<bool> SkipResponseBody = new("ImageGen.SkipResponseBody");

    public static long GetLimit(HttpRequestMessage request) => request.Options.TryGetValue(BodyKind, out var kind)
        ? kind switch
        {
            ProviderResponseBodyKind.GeneratedImageJson => GeneratedImageJson,
            ProviderResponseBodyKind.Image => Image,
            _ => Json,
        }
        : Json;
}
