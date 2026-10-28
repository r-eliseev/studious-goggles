using ImageGen.Core.Domain.Descriptions;

namespace ImageGen.Core.Services;

/// <summary>A description was not made for a reason the user can act on. The message is written by the core and has no secrets.</summary>
public sealed class ImageDescriptionException(DescriptionError error, string message) : Exception(message)
{
    public DescriptionError Error { get; } = error;
}
