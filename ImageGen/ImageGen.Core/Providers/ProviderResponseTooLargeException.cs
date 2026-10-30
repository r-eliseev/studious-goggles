namespace ImageGen.Core.Providers;

public sealed class ProviderResponseTooLargeException(long limitBytes, long? declaredLength = null)
    : Exception("Provider response exceeded the configured size limit.")
{
    public long LimitBytes { get; } = limitBytes;

    public long? DeclaredLength { get; } = declaredLength;
}
