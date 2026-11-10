using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.UI;

namespace ImageGen.Avalonia.Platform;

[Activity(
    Exported = false,
    Theme = "@style/MyTheme.NoActionBar",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize)]
public sealed class MediaPlayerActivity : Activity
{
    private static readonly object Gate = new();
    private static bool _closeRequested;

    public const string FilePathExtra = "imagegen.media.file-path";
    public const string AccessibleLabelExtra = "imagegen.media.accessible-label";

    public static event EventHandler? Closed;

    public static MediaPlayerActivity? Current { get; private set; }

    private IExoPlayer? _player;

    public static void PrepareLaunch()
    {
        lock (Gate)
        {
            _closeRequested = false;
        }
    }

    public static void RequestClose()
    {
        MediaPlayerActivity? activity;
        lock (Gate)
        {
            activity = Current;
            if (activity is null)
            {
                _closeRequested = true;
                return;
            }
        }

        activity.RunOnUiThread(activity.Finish);
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        bool closeRequested;
        lock (Gate)
        {
            Current = this;
            closeRequested = _closeRequested;
            _closeRequested = false;
        }

        if (closeRequested)
        {
            Finish();
            return;
        }

        var filePath = Intent?.GetStringExtra(FilePathExtra);
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            Finish();
            return;
        }

        var playerView = new PlayerView(this)
        {
            UseController = true,
            KeepScreenOn = true,
            ContentDescription = Intent?.GetStringExtra(AccessibleLabelExtra),
        };
        playerView.SetBackgroundColor(Color.Black);
        var player = new ExoPlayerBuilder(this).Build()
            ?? throw new InvalidOperationException("Media3 could not create a player.");
        _player = player;
        playerView.Player = player;
        var mediaUri = Android.Net.Uri.FromFile(new Java.IO.File(filePath))
            ?? throw new InvalidOperationException("Android could not create a media URI.");
        player.SetMediaItem(MediaItem.FromUri(mediaUri));
        player.Prepare();
        player.PlayWhenReady = true;
        SetContentView(playerView, new ViewGroup.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.MatchParent));
    }

    protected override void OnStop()
    {
        _player?.Pause();
        base.OnStop();
    }

    protected override void OnDestroy()
    {
        lock (Gate)
        {
            if (ReferenceEquals(Current, this))
            {
                Current = null;
            }
        }

        _player?.Release();
        _player?.Dispose();
        _player = null;
        base.OnDestroy();
        Closed?.Invoke(this, EventArgs.Empty);
    }
}
