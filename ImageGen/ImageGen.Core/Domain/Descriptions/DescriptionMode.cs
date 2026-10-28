namespace ImageGen.Core.Domain.Descriptions;

public enum DescriptionMode
{
    /// <summary>A description of what the picture shows, in the app's language.</summary>
    Description,

    /// <summary>An English prompt that would reproduce the picture with an image model.</summary>
    Prompt,
}
