using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using ImageGen.Avalonia.Tests.TestDoubles;
using ImageGen.Avalonia.Views;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;

namespace ImageGen.Avalonia.Tests.Views;

public class SessionsViewTests
{
    [AvaloniaFact]
    public async Task Narrow_HidesEmptyState()
    {
        using var shell = new ShellHarness();
        await shell.SaveKeyAsync("sk-or-test");
        await shell.InitializeDatabaseAsync();
        shell.Window.Width = 600;
        shell.Window.Show();
        await shell.Navigation.GoToAsync(Routes.Sessions);
        shell.Window.UpdateLayout();

        var view = Assert.IsType<SessionsView>(shell.Window.PageArea.Content);
        Assert.False(view.FindControl<Border>("EmptyState")!.IsVisible);
    }

    [AvaloniaFact]
    public async Task Wide_ShowsEmptyState()
    {
        using var shell = new ShellHarness();
        await shell.SaveKeyAsync("sk-or-test");
        await shell.InitializeDatabaseAsync();
        shell.Window.Width = 1100;
        shell.Window.Show();
        await shell.Navigation.GoToAsync(Routes.Sessions);
        shell.Window.UpdateLayout();

        var view = Assert.IsType<SessionsView>(shell.Window.PageArea.Content);
        Assert.True(view.FindControl<Border>("EmptyState")!.IsVisible);
    }

    [AvaloniaFact]
    public async Task DeleteButton_HasAutomationName()
    {
        using var shell = new ShellHarness();
        await shell.SaveKeyAsync("sk-or-test");
        await shell.InitializeDatabaseAsync();
        shell.Window.Show();
        await shell.Navigation.GoToAsync(Routes.Sessions);
        var model = shell.Provider.GetRequiredService<SessionsPageModel>();
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Title = "Fox",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Settings = SessionSettings.CreateDefault(),
        };
        model.Today.Add(new SessionItemViewModel(session, _ => Task.CompletedTask, _ => Task.CompletedTask, _ => Task.CompletedTask));
        shell.Window.UpdateLayout();

        var view = Assert.IsType<SessionsView>(shell.Window.PageArea.Content);
        var names = view.GetVisualDescendants().OfType<Button>().Select(b => AutomationProperties.GetName(b)).ToList();
        Assert.Contains(AppResources.DeleteSession, names);
    }

    [AvaloniaFact]
    public async Task RowContextMenu_HasRenameAndDelete()
    {
        using var shell = new ShellHarness();
        await shell.SaveKeyAsync("sk-or-test");
        await shell.InitializeDatabaseAsync();
        shell.Window.Show();
        await shell.Navigation.GoToAsync(Routes.Sessions);
        var model = shell.Provider.GetRequiredService<SessionsPageModel>();
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Title = "Fox",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Settings = SessionSettings.CreateDefault(),
        };
        model.Today.Add(new SessionItemViewModel(session, _ => Task.CompletedTask, _ => Task.CompletedTask, _ => Task.CompletedTask));
        shell.Window.UpdateLayout();

        var view = Assert.IsType<SessionsView>(shell.Window.PageArea.Content);
        var row = view.GetVisualDescendants().OfType<Grid>().Single(grid => grid.Classes.Contains("session-row"));
        var headers = row.ContextMenu!.Items.OfType<MenuItem>().Select(item => item.Header?.ToString()).ToList();
        Assert.Equal([AppResources.Rename, AppResources.DeleteSession], headers);
    }
}
