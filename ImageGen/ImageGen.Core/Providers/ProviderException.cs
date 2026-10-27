using ImageGen.Core.Domain.Generations;

namespace ImageGen.Core.Providers;

/// <summary>Error returned by a provider. The message never contains API keys.</summary>
public sealed class ProviderException : Exception
{
    public ProviderException(ErrorKind kind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    public ErrorKind Kind { get; }
}
