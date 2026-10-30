namespace ImageGen.Core.Storage;

/// <summary>Pixel dimensions read from an image file.</summary>
public readonly record struct ImageDimensions
{
    public ImageDimensions(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }
}
