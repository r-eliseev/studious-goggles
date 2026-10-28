namespace ImageGen.Presentation.Services;

public interface INavigationService
{
    Task GoToAsync(string route);

    Task GoBackAsync();
}
