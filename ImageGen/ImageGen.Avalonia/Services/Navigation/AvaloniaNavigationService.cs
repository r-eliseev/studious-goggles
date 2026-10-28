using Avalonia.Controls;
using ImageGen.Presentation.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Avalonia.Services.Navigation;

/// <summary>
/// Page stack in the main window (Avalonia has no Shell). Routes are the same strings as in MAUI (<c>Routes</c>);
/// going back shows the previous page again, and that page is told it appears (<see cref="IAppearing"/>). A page that is
/// covered or popped is told it disappears (<see cref="IDisappearing"/>).
/// </summary>
public sealed class AvaloniaNavigationService(PageRoutes routes, ILogger<AvaloniaNavigationService>? logger = null) : INavigationService
{
    private readonly ILogger<AvaloniaNavigationService> _logger = logger ?? NullLogger<AvaloniaNavigationService>.Instance;
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
        if (_stack.Count > 0)
        {
            Leave(_stack[^1].View);
        }

        _stack.Add((route, view));
        _logger.LogInformation("Navigation to {Route}.", route);
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
        Leave(_stack[^1].View);
        _stack.RemoveAt(_stack.Count - 1);
        _logger.LogInformation("Navigation back to {Route}.", _stack[^1].Route);
        Show(host, _stack[^1].View);
        return Task.CompletedTask;
    }

    private ContentControl RequireHost() =>
        _host ?? throw new InvalidOperationException("Окно приложения ещё не создано: некуда показать страницу.");

    private static void Leave(Control view)
    {
        if (view is IDisappearing disappearing)
        {
            disappearing.OnDisappearing();
        }
    }

    private static void Show(ContentControl host, Control view)
    {
        host.Content = view;
        if (view is IAppearing appearing)
        {
            appearing.OnAppearing();
        }
    }
}
