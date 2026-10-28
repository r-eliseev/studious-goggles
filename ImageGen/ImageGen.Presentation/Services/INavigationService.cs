namespace ImageGen.Presentation.Services;

public interface INavigationService
{
    Task GoToAsync(string route);

    /// <summary>Shows <paramref name="route"/> in place of the current page, so going back does not return to that page.</summary>
    Task ReplaceAsync(string route);

    Task GoBackAsync();
}
