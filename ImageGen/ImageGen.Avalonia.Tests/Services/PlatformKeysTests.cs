using Avalonia.Input;
using ImageGen.Avalonia.Services;

namespace ImageGen.Avalonia.Tests.Services;

public class PlatformKeysTests
{
    [Fact]
    public void For_MacOs_UsesMeta()
    {
        Assert.Equal(KeyModifiers.Meta, PlatformKeys.For(isMacOs: true));
    }

    [Fact]
    public void For_OtherSystems_UsesControl()
    {
        Assert.Equal(KeyModifiers.Control, PlatformKeys.For(isMacOs: false));
    }

    [Fact]
    public void Label_MacOs_ShowsCommandSign()
    {
        Assert.Equal("⌘N", PlatformKeys.Label("N", isMacOs: true));
    }

    [Fact]
    public void Label_OtherSystems_ShowsCtrl()
    {
        Assert.Equal("Ctrl+N", PlatformKeys.Label("N", isMacOs: false));
    }
}
