using ImageGen.Core.Domain;

namespace ImageGen.Core.Providers;

/// <summary>Error returned by a provider. The message never contains API keys.</summary>
public sealed class ProviderException : Exception
{
    public ProviderException(ErrorKind kind, string message, int? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
        StatusCode = statusCode;
    }

    public ErrorKind Kind { get; }

    public int? StatusCode { get; }
}
