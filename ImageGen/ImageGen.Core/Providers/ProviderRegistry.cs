using System.Text.RegularExpressions;
using ImageGen.Core.Domain.Generations;

namespace ImageGen.Core.Providers;

/// <summary>The providers of this run, validated. Unknown ids are not configured.</summary>
public sealed class ProviderRegistry
{
    private static readonly Regex IdPattern = new("^[a-z0-9][a-z0-9-]*$", RegexOptions.CultureInvariant);
    private readonly Dictionary<string, ProviderConfig> _byId;

    /// <exception cref="ProviderConfigException">A provider is invalid or the id is repeated.</exception>
    public ProviderRegistry(IEnumerable<ProviderConfig> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        var list = providers.ToList();
        _byId = new Dictionary<string, ProviderConfig>(StringComparer.Ordinal);
        foreach (var provider in list)
        {
            Validate(provider);
            if (_byId.ContainsKey(provider.Id))
            {
                throw new ProviderConfigException($"Provider id '{provider.Id}' is defined twice.");
            }

            _byId.Add(provider.Id, provider);
        }

        if (_byId.Count == 0)
        {
            throw new ProviderConfigException("No providers are configured.");
        }

        All = list;
    }

    public IReadOnlyList<ProviderConfig> All { get; }

    /// <exception cref="ProviderException">The id is not configured (kind <see cref="ErrorKind.ModelUnavailable"/>).</exception>
    public ProviderConfig Get(string providerId) =>
        TryGet(providerId, out var provider)
            ? provider
            : throw new ProviderException(ErrorKind.ModelUnavailable, $"Provider '{providerId}' is not configured.");

    public bool TryGet(string providerId, out ProviderConfig provider) =>
        _byId.TryGetValue(providerId ?? string.Empty, out provider!);

    private static void Validate(ProviderConfig provider)
    {
        if (string.IsNullOrEmpty(provider.Id) || !IdPattern.IsMatch(provider.Id))
        {
            throw new ProviderConfigException(
                $"Provider id '{provider.Id}' is invalid: use lowercase letters, digits and '-', starting with a letter or digit.");
        }

        if (string.IsNullOrWhiteSpace(provider.DisplayName))
        {
            throw new ProviderConfigException($"Provider '{provider.Id}': displayName is empty.");
        }

        var address = provider.BaseAddress;
        var isLocalHttp = address.Scheme == Uri.UriSchemeHttp && address.Host == "localhost";
        if (!address.IsAbsoluteUri || (address.Scheme != Uri.UriSchemeHttps && !isLocalHttp))
        {
            throw new ProviderConfigException(
                $"Provider '{provider.Id}': baseAddress must be an absolute https address (http is allowed for localhost only).");
        }

        if (provider.Timeout <= TimeSpan.Zero)
        {
            throw new ProviderConfigException($"Provider '{provider.Id}': timeoutSeconds must be greater than 0.");
        }

        if (provider.Kind != ProviderKind.OpenAiCompatible && provider.ImageModels.Count > 0)
        {
            throw new ProviderConfigException(
                $"Provider '{provider.Id}': imageModels is allowed only for kind openAiCompatible.");
        }

        if ((provider.ImageResponseFormat is not null || provider.ImageOutputFormat is not null)
            && provider.Kind != ProviderKind.OpenAiCompatible)
        {
            throw new ProviderConfigException(
                $"Provider '{provider.Id}': image formats are allowed only for kind openAiCompatible.");
        }

        if (provider.ImageResponseFormat is not null && provider.ImageResponseFormat is not ("base64" or "b64_json"))
        {
            throw new ProviderConfigException($"Provider '{provider.Id}': imageResponseFormat has an unsupported value.");
        }

        if (provider.ImageOutputFormat is not null && provider.ImageOutputFormat is not ("png" or "jpeg"))
        {
            throw new ProviderConfigException($"Provider '{provider.Id}': imageOutputFormat has an unsupported value.");
        }

        foreach (var model in provider.ImageModels)
        {
            if (string.IsNullOrWhiteSpace(model.Id) || model.MaxImagesPerRequest < 1)
            {
                throw new ProviderConfigException(
                    $"Provider '{provider.Id}': each imageModels entry needs an id and maxImagesPerRequest of at least 1.");
            }
        }
    }
}
