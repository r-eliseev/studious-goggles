using Android.Content;
using Android.OS;
using AndroidX.Core.Content;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;

namespace ImageGen.Avalonia.Platform;

/// <summary>Shares temporary copies of application logs through Android's system chooser.</summary>
public sealed class AndroidLogSharer : ILogSharer
{
    public const string FileProviderAuthority = "com.eliseev.imagegen.avalonia.fileprovider";

    public bool IsSupported => true;

    public Task ShareAsync(IReadOnlyList<string> files, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        cancellationToken.ThrowIfCancellationRequested();
        if (files.Count == 0)
        {
            return Task.CompletedTask;
        }

        var activity = MainActivity.Current ?? throw new InvalidOperationException("The Android activity is not available.");
        var context = global::Android.App.Application.Context;
        var uris = files
            .Select(path => FileProvider.GetUriForFile(context, FileProviderAuthority, new Java.IO.File(path))
                ?? throw new InvalidOperationException("Android could not create a log sharing address."))
            .ToList();
        var send = new Intent(Intent.ActionSendMultiple);
        send.SetType("text/plain");
        send.PutParcelableArrayListExtra(Intent.ExtraStream, uris.Select(uri => (IParcelable)uri).ToList());
        send.AddFlags(ActivityFlags.GrantReadUriPermission);

        var clip = ClipData.NewUri(context.ContentResolver, AppResources.SettingsShareLogsTitle, uris[0])
            ?? throw new InvalidOperationException("Android could not create the log sharing attachment list.");
        for (var index = 1; index < uris.Count; index++)
        {
            clip.AddItem(new ClipData.Item(uris[index]));
        }

        send.ClipData = clip;
        activity.StartActivity(Intent.CreateChooser(send, AppResources.SettingsShareLogsTitle));
        return Task.CompletedTask;
    }
}
