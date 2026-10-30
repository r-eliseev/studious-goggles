using ImageGen.Presentation.Services;

namespace ImageGen.Presentation.Tests.Services;

public class ImageViewerNavigationStateTests
{
    [Fact]
    public void TryFail_PreviousRequest_RestoresLastDisplayedIndex()
    {
        var state = new ImageViewerNavigationState(4, 2);
        Assert.True(state.TryCommit(state.InitialRequest));
        Assert.True(state.TryNavigate(-1, out var previous));

        Assert.True(state.TryFail(previous, out _, out var displayed));

        Assert.Equal(2, displayed);
        Assert.True(state.TryNavigate(-1, out var retry));
        Assert.Equal(1, retry.Index);
    }

    [Fact]
    public void TryFail_NextRequest_RestoresLastDisplayedIndex()
    {
        var state = new ImageViewerNavigationState(4, 1);
        Assert.True(state.TryCommit(state.InitialRequest));
        Assert.True(state.TryNavigate(1, out var next));

        Assert.True(state.TryFail(next, out _, out var displayed));

        Assert.Equal(1, displayed);
        Assert.True(state.TryNavigate(1, out var retry));
        Assert.Equal(2, retry.Index);
    }

    [Fact]
    public void TryCommit_StaleRequest_DoesNotReplaceCurrentRequest()
    {
        var state = new ImageViewerNavigationState(3, 0);
        var initial = state.InitialRequest;
        Assert.True(state.TryNavigate(1, out var latest));

        Assert.False(state.TryCommit(initial));
        Assert.False(state.TryFail(initial, out _, out _));
        Assert.True(state.TryCommit(latest));
        Assert.Equal(1, state.DisplayedIndex);
    }

    [Fact]
    public void TryNavigate_OutsideBounds_ReturnsFalseWithoutWrap()
    {
        var state = new ImageViewerNavigationState(2, 0);

        Assert.False(state.TryNavigate(-1, out _));
        Assert.True(state.TryNavigate(1, out var last));
        Assert.False(state.TryNavigate(1, out _));
        Assert.Equal(1, last.Index);
    }

    [Fact]
    public void Close_InvalidatesPendingRequests()
    {
        var state = new ImageViewerNavigationState(2, 0);
        var request = state.InitialRequest;
        state.Close();

        Assert.False(state.TryCommit(request));
        Assert.False(state.TryNavigate(1, out _));
    }
}
