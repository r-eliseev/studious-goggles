namespace ImageGen.Maui.Services;

public interface INavigationService
{
    Task GoToAsync(string route);

    Task GoBackAsync();
}
