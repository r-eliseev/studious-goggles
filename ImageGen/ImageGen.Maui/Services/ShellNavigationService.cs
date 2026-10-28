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

    public Task GoBackAsync()
    {
        logger.LogInformation("Navigation back.");
        return Shell.Current.GoToAsync("..");
    }
}
