namespace ImageGen.Maui.Services;

/// <summary>Runs work on the UI thread. The MAUI implementation wraps <c>MainThread</c>; tests run the action at once.</summary>
public interface IMainThread
{
    void BeginInvokeOnMainThread(Action action);
}
