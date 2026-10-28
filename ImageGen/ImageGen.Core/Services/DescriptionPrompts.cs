using ImageGen.Core.Domain.Descriptions;

namespace ImageGen.Core.Services;

/// <summary>System texts of the description modes. The prompt mode is always English; the description uses the app's language.</summary>
public static class DescriptionPrompts
{
    public const string UserText = "Image:";

    public static string For(DescriptionMode mode, string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        return mode switch
        {
            DescriptionMode.Description =>
                $"Describe the image in {language}: the subject, the setting, the style and the colors. Plain text, no markdown.",
            DescriptionMode.Prompt =>
                "Write one English prompt that would reproduce this image with an image model: subject, style, composition and lighting. Plain text, no markdown.",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
    }
}
