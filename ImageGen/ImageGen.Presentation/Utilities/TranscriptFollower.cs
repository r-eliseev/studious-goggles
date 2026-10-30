namespace ImageGen.Presentation.Utilities;

/// <summary>Tracks whether transcript content and viewport changes should keep the last row visible.</summary>
public sealed class TranscriptFollower
{
    public const double EndTolerance = 24;

    public bool IsFollowing { get; private set; } = true;

    public bool RowsChanged()
    {
        IsFollowing = true;
        return true;
    }

    public bool ContentChanged() => IsFollowing;

    public bool ViewportChanged() => IsFollowing;

    public void UserScrolled(bool atEnd) => IsFollowing = atEnd;

    public static bool IsAtEnd(double offset, double viewport, double extent) =>
        offset + viewport >= extent - EndTolerance;
}
