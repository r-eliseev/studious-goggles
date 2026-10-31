using AndroidX.RecyclerView.Widget;

namespace ImageGen.Maui.Platforms.Android;

/// <summary>
/// Scrolls the chat transcript to the bottom of its content on Android (task 159). A run row is often taller than the
/// transcript, and <c>CollectionView.ScrollTo(…, End)</c> then stops at the top of that row; here the list first shows
/// the last row and then scrolls on until it cannot go further down.
/// </summary>
public static class TranscriptScroller
{
    /// <summary>Upper bound of the scroll steps, one transcript height each, so a list that keeps growing cannot loop.</summary>
    private const int MaxSteps = 64;

    /// <summary>Returns false when the view is not a ready list: the caller falls back to <c>ScrollTo</c>.</summary>
    public static bool ScrollToBottom(object? platformView)
    {
        if (platformView is not RecyclerView list || list.GetAdapter() is not { ItemCount: > 0 } adapter)
        {
            return false;
        }

        list.ScrollToPosition(adapter.ItemCount - 1);
        // The last row is laid out only after this pass, so the rest of the way down is scrolled on the next one.
        list.Post(() => ScrollRest(list));
        return true;
    }

    /// <summary>True when the list cannot scroll further down; null when the view is not a list.</summary>
    public static bool? IsAtBottom(object? platformView) =>
        platformView is RecyclerView list ? !list.CanScrollVertically(1) : null;

    private static void ScrollRest(RecyclerView list)
    {
        for (var step = 0; step < MaxSteps && list.CanScrollVertically(1); step++)
        {
            list.ScrollBy(0, Math.Max(list.Height, 1));
        }
    }
}
