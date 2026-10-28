namespace ImageGen.Core.Providers;

/// <summary>
/// The provider config is invalid. The message names the provider and the field, never a key value;
/// the app does not start with an invalid config.
/// </summary>
public sealed class ProviderConfigException : Exception
{
    public ProviderConfigException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
