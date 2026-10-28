using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Services;
using NSubstitute;

namespace ImageGen.Presentation.Tests.PageModels;

public class CopyTextActionTests
{
    private readonly IClipboardService _clipboard = Substitute.For<IClipboardService>();
    private readonly IMainThread _mainThread = Substitute.For<IMainThread>();

    public CopyTextActionTests()
    {
        _mainThread.When(m => m.BeginInvokeOnMainThread(Arg.Any<Action>()))
            .Do(ci => ci.Arg<Action>()());
    }

    [Fact]
    public async Task CopyCommand_PutsTextOnClipboardAndShowsCopied()
    {
        var action = new CopyTextAction(_clipboard, _mainThread, "red fox");

        await action.CopyCommand.ExecuteAsync(null);

        await _clipboard.Received(1).SetTextAsync("red fox");
        Assert.True(action.IsCopied);
    }

    [Fact]
    public async Task CopyCommand_CopiedDisappearsAfterDelay()
    {
        var action = new CopyTextAction(_clipboard, _mainThread, "red fox", TimeSpan.FromMilliseconds(20));

        await action.CopyCommand.ExecuteAsync(null);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (action.IsCopied && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.False(action.IsCopied);
    }

    [Fact]
    public void Constructor_NullText_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new CopyTextAction(_clipboard, _mainThread, null!));
    }
}
