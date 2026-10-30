using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ImageGen.Avalonia.Services;

namespace ImageGen.Avalonia.Tests.Services;

public sealed class AvaloniaFullScreenControllerTests
{
    [AvaloniaFact]
    public void Enter_ChangesViewerOnly_AndDisposeRestoresStateOnce()
    {
        var owner = new Window { WindowState = WindowState.Maximized, WindowDecorations = WindowDecorations.Full };
        var viewer = new Window { WindowState = WindowState.Normal, WindowDecorations = WindowDecorations.None };
        var controller = new AvaloniaFullScreenController();

        var lease = controller.Enter(viewer);

        Assert.Equal(WindowState.FullScreen, viewer.WindowState);
        Assert.Equal(WindowState.Maximized, owner.WindowState);
        Assert.Equal(WindowDecorations.Full, owner.WindowDecorations);
        lease.Dispose();
        lease.Dispose();
        Assert.Equal(WindowState.Normal, viewer.WindowState);
        Assert.Equal(WindowDecorations.None, viewer.WindowDecorations);
    }
}
