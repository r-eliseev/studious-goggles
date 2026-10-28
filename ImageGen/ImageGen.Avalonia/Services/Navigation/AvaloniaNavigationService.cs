using Avalonia.Controls;
using ImageGen.Presentation.Services;

namespace ImageGen.Avalonia.Services.Navigation;

/// <summary>
/// Page stack in the main window (Avalonia has no Shell). Routes are the same strings as in MAUI (<c>Routes</c>);
/// going back shows the previous page again, and that page is told it appears (<see cref="IAppearing"/>).
/// </summary>
public sealed class AvaloniaNavigationService(PageRoutes routes) : INavigationService
{
    private readonly List<(string Route, Control View)> _stack = [];
    private ContentControl? _host;

    /// <summary>The route of the page on top of the stack, or null before the first navigation.</summary>
    public string? CurrentRoute => _stack.Count == 0 ? null : _stack[^1].Route;

    /// <summary>Sets the control that shows the pages (the main window's page host).</summary>
    public void AttachHost(ContentControl host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
    }

    public Task GoToAsync(string route)
    {
        var (path, query) = RouteParser.Parse(route);
        var create = routes.Find(path) ?? throw new InvalidOperationException($"Неизвестный маршрут: {path}");
        var host = RequireHost();

        var view = create(query);
        _stack.Add((route, view));
        Show(host, view);
        return Task.CompletedTask;
    }

    public Task GoBackAsync()
    {
        if (_stack.Count < 2)
        {
            return Task.CompletedTask;
        }

        var host = RequireHost();
        _stack.RemoveAt(_stack.Count - 1);
        Show(host, _stack[^1].View);
        return Task.CompletedTask;
    }

    private ContentControl RequireHost() =>
        _host ?? throw new InvalidOperationException("Окно приложения ещё не создано: некуда показать страницу.");

    private static void Show(ContentControl host, Control view)
    {
        host.Content = view;
        if (view is IAppearing appearing)
        {
            appearing.OnAppearing();
        }
    }
}
