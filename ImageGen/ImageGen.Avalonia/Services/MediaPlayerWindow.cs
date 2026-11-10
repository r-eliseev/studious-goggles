using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using LibVLCSharp.Avalonia;

namespace ImageGen.Avalonia.Services;

internal sealed class MediaPlayerWindow : Window
{
    private readonly IMediaPlayback _playback;
    private readonly Slider _position;
    private readonly TextBlock _time;
    private readonly DispatcherTimer _timer;
    private bool _seeking;

    public MediaPlayerWindow(IMediaPlayback playback, string title, bool isVideo, Window owner)
    {
        _playback = playback;
        Title = title;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = Math.Max(owner.Bounds.Width, 640);
        Height = Math.Max(owner.Bounds.Height, 480);
        MinWidth = 480;
        MinHeight = 320;

        _position = new Slider { Minimum = 0, Maximum = 1000, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(_position, "Playback position");
        _position.PointerPressed += (_, _) => _seeking = true;
        _position.PointerReleased += (_, _) =>
        {
            Seek();
            _seeking = false;
        };
        _time = new TextBlock { VerticalAlignment = VerticalAlignment.Center, MinWidth = 95 };
        var toggle = new Button { Content = "Play / Pause", MinWidth = 48, MinHeight = 48 };
        AutomationProperties.SetName(toggle, "Play or pause");
        toggle.Click += (_, _) => TogglePlayback();
        var volume = new Slider { Minimum = 0, Maximum = 100, Value = 100, Width = 120, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(volume, "Volume");
        volume.ValueChanged += (_, args) => _playback.Volume = (int)args.NewValue;
        var controls = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), MinHeight = 64, Margin = new global::Avalonia.Thickness(12, 4) };
        Grid.SetColumn(toggle, 0);
        Grid.SetColumn(_position, 1);
        Grid.SetColumn(_time, 2);
        Grid.SetColumn(volume, 3);
        controls.Children.Add(toggle);
        controls.Children.Add(_position);
        controls.Children.Add(_time);
        controls.Children.Add(volume);

        var root = new DockPanel();
        DockPanel.SetDock(controls, Dock.Bottom);
        root.Children.Add(controls);
        if (isVideo && playback.VideoPlayer is { } player)
            root.Children.Insert(0, new VideoView { MediaPlayer = player, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch });
        Content = root;

        KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape) Close();
            else if (args.Key == Key.Space) TogglePlayback();
        };
        Opened += (_, _) => Focus();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        Closed += (_, _) => _timer.Stop();
        _timer.Tick += (_, _) => UpdatePosition();
        _timer.Start();
        UpdatePosition();
    }

    private void TogglePlayback()
    {
        if (_playback.IsPlaying) _playback.Pause();
        else _playback.Play();
    }

    private void Seek()
    {
        if (_playback.Length > 0) _playback.Time = (long)(_playback.Length * _position.Value / _position.Maximum);
    }

    private void UpdatePosition()
    {
        var length = Math.Max(0, _playback.Length);
        var time = Math.Clamp(_playback.Time, 0, length);
        if (!_seeking && length > 0) _position.Value = time * _position.Maximum / length;
        _time.Text = $"{FormatTime(time)} / {FormatTime(length)}";
    }

    private static string FormatTime(long milliseconds)
    {
        var time = TimeSpan.FromMilliseconds(milliseconds);
        return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");
    }
}
