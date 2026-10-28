using Avalonia.Threading;
using ImageGen.Presentation.Services;

namespace ImageGen.Avalonia.Services;

public sealed class AvaloniaMainThread : IMainThread
{
    public void BeginInvokeOnMainThread(Action action) => Dispatcher.UIThread.Post(action);
}
