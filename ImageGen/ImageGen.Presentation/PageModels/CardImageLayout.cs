namespace ImageGen.Presentation.PageModels;

public static class CardImageLayout
{
    private const double Width = 264;
    private const double DefaultHeight = 176;
    private const double MinimumHeight = 132;
    private const double MaximumHeight = 352;

    public static double HeightFor(int? pixelWidth, int? pixelHeight)
    {
        if (pixelWidth is not > 0 || pixelHeight is not > 0)
        {
            return DefaultHeight;
        }

        return Math.Clamp(Width * pixelHeight.Value / pixelWidth.Value, MinimumHeight, MaximumHeight);
    }
}
