namespace ImageGen.Maui.Services;

/// <summary>Leaves OS chrome unchanged on platforms without a viewer fullscreen implementation.</summary>
public sealed class NoOpFullScreenController : IMauiFullScreenController
{
    public IDisposable Enter(Microsoft.Maui.Controls.Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return EmptyLease.Instance;
    }

    private sealed class EmptyLease : IDisposable
    {
        public static EmptyLease Instance { get; } = new();

        public void Dispose() { }
    }
}
