using Microsoft.Maui.ApplicationModel;

namespace ImageGen.Maui.Services;

public sealed class MauiMainThread : IMainThread
{
    public void BeginInvokeOnMainThread(Action action) => MainThread.BeginInvokeOnMainThread(action);
}
