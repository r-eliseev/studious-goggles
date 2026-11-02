using ImageGen.Core.Domain.Generations;

namespace ImageGen.Core.Providers;

/// <summary>Error returned by a provider. The message never contains API keys.</summary>
public sealed class ProviderException : Exception
{
    public ProviderException(ErrorKind kind, string message, Exception? innerException = null, int? statusCode = null)
        : base(message, innerException)
    {
        Kind = kind;
        StatusCode = statusCode;
    }

    public ErrorKind Kind { get; }

    public int? StatusCode { get; }
}
