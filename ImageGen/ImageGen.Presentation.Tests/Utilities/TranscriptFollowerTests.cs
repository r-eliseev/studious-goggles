using ImageGen.Presentation.Utilities;

namespace ImageGen.Presentation.Tests.Utilities;

public class TranscriptFollowerTests
{
    [Fact]
    public void New_IsFollowing()
    {
        var follower = new TranscriptFollower();

        Assert.True(follower.IsFollowing);
        Assert.True(follower.ContentChanged());
        Assert.True(follower.ViewportChanged());
    }

    [Fact]
    public void UserScrolledUp_ContentChanged_DoesNotScroll()
    {
        var follower = new TranscriptFollower();

        follower.UserScrolled(false);

        Assert.False(follower.ContentChanged());
        Assert.False(follower.ViewportChanged());
    }

    [Fact]
    public void UserScrolledUp_RowsChanged_ScrollsAndFollows()
    {
        var follower = new TranscriptFollower();
        follower.UserScrolled(false);

        Assert.True(follower.RowsChanged());
        Assert.True(follower.IsFollowing);
    }

    [Fact]
    public void UserScrolledBackToEnd_FollowsAgain()
    {
        var follower = new TranscriptFollower();
        follower.UserScrolled(false);

        follower.UserScrolled(true);

        Assert.True(follower.ContentChanged());
    }

    [Fact]
    public void IsAtEnd_WithinTolerance_True() => Assert.True(TranscriptFollower.IsAtEnd(976, 1000, 2000));

    [Fact]
    public void IsAtEnd_AboveTolerance_False() => Assert.False(TranscriptFollower.IsAtEnd(900, 1000, 2000));

    [Fact]
    public void IsAtEnd_ContentShorterThanViewport_True() => Assert.True(TranscriptFollower.IsAtEnd(0, 1000, 500));
}
