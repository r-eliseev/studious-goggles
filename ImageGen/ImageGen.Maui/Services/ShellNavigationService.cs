using ImageGen.Presentation.Services;
using Microsoft.Extensions.Logging;

namespace ImageGen.Maui.Services;

public sealed class ShellNavigationService(ILogger<ShellNavigationService> logger) : INavigationService
{
    public Task GoToAsync(string route)
    {
        logger.LogInformation("Navigation to {Route}.", route);
        return Shell.Current.GoToAsync(route);
    }

    /// <summary>Shell has no in-place replace: the current page is popped and the route is pushed, so back returns to the page below.</summary>
    public async Task ReplaceAsync(string route)
    {
        logger.LogInformation("Navigation replaces the current page with {Route}.", route);
        await Shell.Current.GoToAsync("..");
        await Shell.Current.GoToAsync(route);
    }

    public Task GoBackAsync()
    {
        logger.LogInformation("Navigation back.");
        return Shell.Current.GoToAsync("..");
    }
}
