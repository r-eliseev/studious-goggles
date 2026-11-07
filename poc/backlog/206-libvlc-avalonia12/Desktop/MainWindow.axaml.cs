using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using LibVLCSharp.Shared;

namespace Desktop;

public sealed partial class MainWindow : Window
{
    private readonly LibVLC _libVlc;

    public MainWindow()
    {
        InitializeComponent();
        Core.Initialize();
        _libVlc = new LibVLC();
        this.FindControl<LibVLCSharp.Avalonia.VideoView>("Video")!.MediaPlayer = new MediaPlayer(_libVlc);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
