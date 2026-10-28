using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Avalonia.Views;

namespace ImageGen.Avalonia.Tests.Services.Navigation;

public class AvaloniaNavigationServiceTests
{
    private static PageRoutes Routes() => new PageRoutes()
        .Add("first", _ => new CountingPage())
        .Add("second", _ => new CountingPage());

    [AvaloniaFact]
    public async Task GoToAsync_KnownRoute_ShowsPageAndCallsOnAppearing()
    {
        var host = new ContentControl();
        var navigation = new AvaloniaNavigationService(Routes());
        navigation.AttachHost(host);

        await navigation.GoToAsync("first");

        var page = Assert.IsType<CountingPage>(host.Content);
        Assert.Equal(1, page.Appearances);
        Assert.Equal("first", navigation.CurrentRoute);
    }

    [AvaloniaFact]
    public async Task GoBackAsync_WithOnlyOnePage_KeepsCurrentPage()
    {
        var host = new ContentControl();
        var navigation = new AvaloniaNavigationService(Routes());
        navigation.AttachHost(host);
        await navigation.GoToAsync("first");
        var first = host.Content;

        await navigation.GoBackAsync();

        Assert.Same(first, host.Content);
        Assert.Equal("first", navigation.CurrentRoute);
    }

    [AvaloniaFact]
    public async Task GoBackAsync_WithTwoPages_ShowsPreviousPageAgainAndCallsOnAppearing()
    {
        var host = new ContentControl();
        var navigation = new AvaloniaNavigationService(Routes());
        navigation.AttachHost(host);
        await navigation.GoToAsync("first");
        var first = Assert.IsType<CountingPage>(host.Content);
        await navigation.GoToAsync("second");

        await navigation.GoBackAsync();

        Assert.Same(first, host.Content);
        Assert.Equal(2, first.Appearances);
        Assert.Equal("first", navigation.CurrentRoute);
    }

    [AvaloniaFact]
    public async Task GoToAsync_UnknownRoute_Throws()
    {
        var navigation = new AvaloniaNavigationService(Routes());
        navigation.AttachHost(new ContentControl());

        await Assert.ThrowsAsync<InvalidOperationException>(() => navigation.GoToAsync("missing"));
    }

    [AvaloniaFact]
    public async Task GoToAsync_WithoutHost_Throws()
    {
        var navigation = new AvaloniaNavigationService(Routes());

        await Assert.ThrowsAsync<InvalidOperationException>(() => navigation.GoToAsync("first"));
    }

    private sealed class CountingPage : UserControl, IAppearing
    {
        public int Appearances { get; private set; }

        public void OnAppearing() => Appearances++;
    }
}
