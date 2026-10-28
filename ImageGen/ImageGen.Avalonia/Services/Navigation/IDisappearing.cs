namespace ImageGen.Avalonia.Services.Navigation;

/// <summary>A page that is left: navigation calls it when the page is covered by another page or popped.</summary>
public interface IDisappearing
{
    void OnDisappearing();
}
