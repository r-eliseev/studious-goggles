using CommunityToolkit.Mvvm.ComponentModel;

namespace ImageGen.Presentation.PageModels;

/// <summary>Shared comparison state; shells apply its normalized zoom and pan to both images.</summary>
public sealed partial class ComparisonPageModel : ObservableObject
{
    public const double MinimumZoom = 1;
    public const double MaximumZoom = 8;

    public ComparisonPageModel(ComparisonImage first, ComparisonImage second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        if (first.GenerationId == second.GenerationId)
        {
            throw new ArgumentException("Comparison requires two different generations.", nameof(second));
        }
        First = first;
        Second = second;
    }

    public ComparisonImage First { get; }
    public ComparisonImage Second { get; }

    [ObservableProperty] public partial ComparisonMode Mode { get; set; } = ComparisonMode.SideBySide;
    [ObservableProperty] public partial double Zoom { get; private set; } = MinimumZoom;
    [ObservableProperty] public partial double OffsetX { get; private set; }
    [ObservableProperty] public partial double OffsetY { get; private set; }
    [ObservableProperty] public partial double SliderFraction { get; set; } = 0.5;

    partial void OnSliderFractionChanged(double value) =>
        SliderFraction = Math.Clamp(double.IsFinite(value) ? value : 0.5, 0, 1);

    public void SetZoom(double zoom)
    {
        Zoom = Math.Clamp(double.IsFinite(zoom) ? zoom : MinimumZoom, MinimumZoom, MaximumZoom);
        if (Zoom == MinimumZoom)
        {
            OffsetX = 0;
            OffsetY = 0;
        }
        else
        {
            OffsetX = Math.Clamp(OffsetX, -0.5, 0.5);
            OffsetY = Math.Clamp(OffsetY, -0.5, 0.5);
        }
    }

    public void SetOffset(double x, double y)
    {
        if (Zoom == MinimumZoom)
        {
            OffsetX = 0;
            OffsetY = 0;
            return;
        }
        OffsetX = Math.Clamp(double.IsFinite(x) ? x : 0, -0.5, 0.5);
        OffsetY = Math.Clamp(double.IsFinite(y) ? y : 0, -0.5, 0.5);
    }
}
