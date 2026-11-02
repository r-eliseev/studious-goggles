using ImageGen.Core.Domain.Models;
using ImageGen.Core.Providers;

namespace ImageGen.LiveCheck;

/// <summary>Small valid raster inputs for the reference-image and image-description scenarios.</summary>
public static class LiveCheckAssets
{
    public const string ReferencePngDataUrl =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAYAAADED76LAAAAD0lEQVR4nGP4TwAwjAwFAIS1/wF0QtmAAAAAAElFTkSuQmCC";

    public static ImageGenerationRequest CreateReferenceImageRequest(string modelId, string prompt, int count) =>
        new(modelId, prompt, null, ReferenceImageDataUrls: CreateReferenceImageDataUrls(count));

    public static string[] CreateReferenceImageDataUrls(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        return Enumerable.Repeat(ReferencePngDataUrl, count).ToArray();
    }

    public static AssistantChatRequest CreateDescriptionRequest(string modelId) =>
        new(modelId,
            [AssistantChatMessage.User("Describe this image in one sentence.") with { ImageDataUrls = [ReferencePngDataUrl] }],
            []);
}
