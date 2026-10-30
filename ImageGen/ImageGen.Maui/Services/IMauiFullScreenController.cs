namespace ImageGen.Maui.Services;

/// <summary>Temporarily applies immersive mode to the platform window hosting the image viewer.</summary>
public interface IMauiFullScreenController
{
    IDisposable Enter(Microsoft.Maui.Controls.Window window);
}
