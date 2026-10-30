using ImageGen.Core.Providers;
using NSubstitute;

namespace ImageGen.Presentation.Tests.TestDoubles;

/// <summary>Provider router for page model tests: one provider (OpenRouter by default) answers for every call.</summary>
public static class ProviderDoubles
{
    public static IProviderRouter Router(
        IModelProvider? images = null,
        IAssistantChatClient? chat = null,
        string providerId = ProviderIds.OpenRouter,
        ISynchronousImageTransport? transport = null)
    {
        var imageProvider = images ?? Substitute.For<IModelProvider>();
        var router = Substitute.For<IProviderRouter>();
        router.ImageProviderIds.Returns([providerId]);
        router.GetConnection(providerId).Returns(imageProvider);
        router.GetImageProvider(providerId).Returns(imageProvider);
        router.GetQueuedImageProvider(providerId).Returns(new InMemoryQueuedImageProvider(
            transport ?? Substitute.For<ISynchronousImageTransport>(), providerId, TimeProvider.System));
        router.GetChatClient(providerId).Returns(chat ?? Substitute.For<IAssistantChatClient>());
        return router;
    }

    /// <summary>Registry of the built-in providers (OpenRouter only).</summary>
    public static ProviderRegistry BuiltInRegistry() =>
        ProviderConfigLoader.Build(ProviderConfigLoader.ReadBuiltInJson(), null);
}
