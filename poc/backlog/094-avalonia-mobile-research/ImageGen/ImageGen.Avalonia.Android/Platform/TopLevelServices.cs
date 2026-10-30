using Avalonia.Controls;
using global::Avalonia.Media;
using global::Avalonia.Layout;
using global::Avalonia;
using Avalonia.Input;
using Avalonia.Automation;
using Avalonia.Platform.Storage;
using ImageGen.Presentation.Services;
using ImageGen.Presentation.Resources.Strings;
using System.Globalization;
using Android.Content;
using Android.Provider;
using System.Runtime.Versioning;

namespace ImageGen.Avalonia.Platform;

public sealed class TopLevelClipboardService : IClipboardService
{
    public async Task SetTextAsync(string text)
    {
        var clipboard = TopLevel.GetTopLevel(MobileApp.CurrentView)?.Clipboard;
        if (clipboard is not null)
        {
            var data = new DataTransfer();
            data.Add(DataTransferItem.CreateText(text));
            await clipboard.SetDataAsync(data);
        }
    }
}

public sealed class TopLevelFilePickerService : IFilePickerService
{
    public async Task<PickedImage?> PickImageAsync(CancellationToken cancellationToken)
    {
        var top = TopLevel.GetTopLevel(MobileApp.CurrentView);
        if (top is null) return null;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = AppResources.AttachImageButton,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Images") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp"] }],
        });
        var file = files.FirstOrDefault();
        if (file is null) return null;
        await using var stream = await file.OpenReadAsync();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        var mediaType = Path.GetExtension(file.Name).ToLowerInvariant() switch { ".jpg" or ".jpeg" => "image/jpeg", ".webp" => "image/webp", _ => "image/png" };
        return new PickedImage(memory.ToArray(), mediaType, file.Name);
    }
}

public sealed class MobileFolderLauncher : IFolderLauncher
{
    public bool IsSupported => false;
    public Task OpenAsync(string path, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class AndroidImageSaver : IImageSaver
{
    private const string GalleryFolder = "Pictures/ImageGen";

    public string ActionLabel => AppResources.SaveToGallery;

    public async Task<ImageSaveResult> SaveAsync(string sourcePath, string suggestedFileName, string mediaType, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            return ImageSaveResult.Failed;
        }

        var resolver = Android.App.Application.Context.ContentResolver;
        if (resolver is null)
        {
            return ImageSaveResult.Failed;
        }

        var values = new ContentValues();
        values.Put(MediaStore.IMediaColumns.DisplayName, suggestedFileName);
        values.Put(MediaStore.IMediaColumns.MimeType, mediaType);
        values.Put(MediaStore.IMediaColumns.RelativePath, GalleryFolder);
        values.Put(MediaStore.IMediaColumns.IsPending, 1);
        var uri = resolver.Insert(MediaStore.Images.Media.ExternalContentUri!, values);
        if (uri is null)
        {
            return ImageSaveResult.Failed;
        }

        try
        {
            using var target = resolver.OpenOutputStream(uri) ?? throw new IOException("MediaStore did not open the file.");
            await using var source = File.OpenRead(sourcePath);
            await source.CopyToAsync(target, cancellationToken);
        }
        catch (IOException)
        {
            resolver.Delete(uri, null, null);
            return ImageSaveResult.Failed;
        }
        catch (OperationCanceledException)
        {
            resolver.Delete(uri, null, null);
            throw;
        }

        var published = new ContentValues();
        published.Put(MediaStore.IMediaColumns.IsPending, 0);
        resolver.Update(uri, published, null, null);
        return ImageSaveResult.Saved;
    }
}

public sealed class OverlayDialogService : IDialogService
{
    private static TaskCompletionSource<string?>? currentAnswer;

    public static void CancelOpenDialog() => currentAnswer?.TrySetResult(null);

    private static async Task<string?> Show(string title, string message, string? accept = null, string? cancel = null, string? initial = null, IReadOnlyList<string>? options = null)
    {
        var root = MobileApp.CurrentView?.DialogArea;
        if (root is null) return null;
        var answer = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        currentAnswer = answer;
        var panel = new StackPanel { Spacing = 10, MaxWidth = 360 };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 20, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        TextBox? input = null;
        if (initial is not null) { input = new TextBox { Text = initial }; AutomationProperties.SetName(input, message); panel.Children.Add(input); }
        if (options is not null)
        {
            foreach (var option in options)
            {
                var button = new Button { Content = option };
                AutomationProperties.SetName(button, option);
                button.Click += (_, _) => answer.TrySetResult(option);
                panel.Children.Add(button);
            }
        }
        var buttons = new StackPanel { Orientation = global::Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
        if (accept is not null)
        {
            var ok = new Button { Content = accept };
            AutomationProperties.SetName(ok, accept);
            ok.Click += (_, _) => answer.TrySetResult(input?.Text ?? accept);
            buttons.Children.Add(ok);
        }
        if (cancel is not null)
        {
            var no = new Button { Content = cancel };
            AutomationProperties.SetName(no, cancel);
            no.Click += (_, _) => answer.TrySetResult(null);
            buttons.Children.Add(no);
        }
        panel.Children.Add(buttons);
        root.Children.Clear();
        root.Children.Add(new Border { Background = Brushes.White, Padding = new Thickness(20), Child = panel, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 400 });
        root.IsVisible = true;
        var result = await answer.Task;
        if (ReferenceEquals(currentAnswer, answer)) currentAnswer = null;
        root.IsVisible = false;
        root.Children.Clear();
        return result;
    }
    public async Task AlertAsync(string title, string message) => _ = await Show(title, message, AppResources.Ok);
    public async Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) => await Show(title, message, accept, cancel) is not null;
    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel, bool destructive) => ConfirmAsync(title, message, accept, cancel);
    public Task<string?> PromptAsync(string title, string message, string accept, string cancel, string initialValue) => Show(title, message, accept, cancel, initialValue);
    public Task<string?> ChooseAsync(string title, string cancel, IReadOnlyList<string> options) => Show(title, "", null, cancel, options: options);
}
