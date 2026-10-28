namespace ImageGen.Core.Domain.Descriptions;

public enum DescriptionError
{
    /// <summary>The assistant model does not take images, so no request is sent.</summary>
    ModelDoesNotAcceptImages,

    /// <summary>The generation's image file is gone, so no request is sent.</summary>
    SourceMissing,

    /// <summary>The model answered with no text; nothing is saved.</summary>
    EmptyResponse,
}
