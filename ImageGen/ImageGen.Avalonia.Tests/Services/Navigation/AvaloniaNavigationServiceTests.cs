using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Avalonia.Views;
using Microsoft.Extensions.Logging;

namespace ImageGen.Avalonia.Tests.Services.Navigation;

public class AvaloniaNavigationServiceTests
{
    private static PageRoutes Routes() => new PageRoutes()
        .Add("first", _ => new CountingPage())
        .Add("second", _ => new CountingPage());

    [AvaloniaFact]
    public async Task GoToAsync_KnownRoute_LogsRouteAtInformation()
    {
        var logger = new CapturingLogger<AvaloniaNavigationService>();
        var host = new ContentControl();
        var navigation = new AvaloniaNavigationService(Routes(), logger);
        navigation.AttachHost(host);

        await navigation.GoToAsync("first");

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("first", entry.Message, StringComparison.Ordinal);
    }

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

    [AvaloniaFact]
    public async Task GoToAsync_LeavingPage_CallsOnDisappearingOfThatPage()
    {
        var host = new ContentControl();
        var navigation = new AvaloniaNavigationService(new PageRoutes()
            .Add("first", _ => new DisappearingPage())
            .Add("second", _ => new CountingPage()));
        navigation.AttachHost(host);
        await navigation.GoToAsync("first");
        var first = Assert.IsType<DisappearingPage>(host.Content);

        await navigation.GoToAsync("second");

        Assert.Equal(1, first.Disappearances);
    }

    [AvaloniaFact]
    public async Task GoBackAsync_PoppedPage_CallsOnDisappearing()
    {
        var host = new ContentControl();
        var navigation = new AvaloniaNavigationService(new PageRoutes()
            .Add("first", _ => new CountingPage())
            .Add("second", _ => new DisappearingPage()));
        navigation.AttachHost(host);
        await navigation.GoToAsync("first");
        await navigation.GoToAsync("second");
        var second = Assert.IsType<DisappearingPage>(host.Content);

        await navigation.GoBackAsync();

        Assert.Equal(1, second.Disappearances);
    }

    private sealed class DisappearingPage : UserControl, IDisappearing
    {
        public int Disappearances { get; private set; }

        public void OnDisappearing() => Disappearances++;
    }

    private sealed class CountingPage : UserControl, IAppearing
    {
        public int Appearances { get; private set; }

        public void OnAppearing() => Appearances++;
    }
}
