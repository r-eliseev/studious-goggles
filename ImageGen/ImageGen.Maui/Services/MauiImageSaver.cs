using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
#if ANDROID
using Android.Content;
using Android.Media;
using Android.Provider;
using Microsoft.Maui.ApplicationModel;
using System.Runtime.Versioning;
#elif IOS && !MACCATALYST
using Foundation;
using Photos;
#else
using CommunityToolkit.Maui.Storage;
#endif

namespace ImageGen.Maui.Services;

/// <summary>
/// Saves a picture where the platform keeps pictures (task 080). Android 10+ writes to the gallery through MediaStore
/// without a permission; older Android writes to Pictures/ImageGen with the storage permission (up to API 28). iOS adds
/// the picture to Photos. Windows and Mac Catalyst show the system save dialog.
/// </summary>
public sealed class MauiImageSaver : IImageSaver
{
#if ANDROID
    private const string GalleryFolder = "Pictures/ImageGen";

    public string ActionLabel => AppResources.SaveToGallery;

    public async Task<ImageSaveResult> SaveAsync(
        string sourcePath, string suggestedFileName, string mediaType, CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            return await SaveToMediaStoreAsync(sourcePath, suggestedFileName, mediaType, cancellationToken);
        }

        return await SaveToPictureFolderAsync(sourcePath, suggestedFileName, mediaType);
    }

    [SupportedOSPlatform("android29.0")]
    private static async Task<ImageSaveResult> SaveToMediaStoreAsync(
        string sourcePath, string suggestedFileName, string mediaType, CancellationToken cancellationToken)
    {
        var resolver = Platform.AppContext.ContentResolver;
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
            using var source = File.OpenRead(sourcePath);
            await source.CopyToAsync(target, cancellationToken);
        }
        catch (IOException)
        {
            resolver.Delete(uri, null, null);
            return ImageSaveResult.Failed;
        }

        // The file stays hidden until the write is complete.
        var published = new ContentValues();
        published.Put(MediaStore.IMediaColumns.IsPending, 0);
        resolver.Update(uri, published, null, null);
        return ImageSaveResult.Saved;
    }

    private static async Task<ImageSaveResult> SaveToPictureFolderAsync(string sourcePath, string suggestedFileName, string mediaType)
    {
        var status = await Permissions.RequestAsync<Permissions.StorageWrite>();
        if (status != PermissionStatus.Granted)
        {
            return ImageSaveResult.PermissionDenied;
        }

        var pictures = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryPictures)?.AbsolutePath
            ?? throw new IOException("The Pictures folder is not available.");
        var folder = Path.Combine(pictures, "ImageGen");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, suggestedFileName);
        File.Copy(sourcePath, path, overwrite: true);
        MediaScannerConnection.ScanFile(Platform.AppContext, [path], [mediaType], null);
        return ImageSaveResult.Saved;
    }
#elif IOS && !MACCATALYST
    public string ActionLabel => AppResources.SaveToPhotos;

    public async Task<ImageSaveResult> SaveAsync(
        string sourcePath, string suggestedFileName, string mediaType, CancellationToken cancellationToken)
    {
        var access = await PHPhotoLibrary.RequestAuthorizationAsync(PHAccessLevel.AddOnly);
        if (access != PHAuthorizationStatus.Authorized)
        {
            return ImageSaveResult.PermissionDenied;
        }

        var completion = new TaskCompletionSource<ImageSaveResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var url = NSUrl.FromFilename(sourcePath);
        PHPhotoLibrary.SharedPhotoLibrary.PerformChanges(
            () => PHAssetChangeRequest.FromImage(url),
            (success, _) => completion.TrySetResult(success ? ImageSaveResult.Saved : ImageSaveResult.Failed));
        return await completion.Task.WaitAsync(cancellationToken);
    }
#else
    public string ActionLabel => AppResources.SaveImageAs;

    public async Task<ImageSaveResult> SaveAsync(
        string sourcePath, string suggestedFileName, string mediaType, CancellationToken cancellationToken)
    {
        using var source = File.OpenRead(sourcePath);
        var result = await FileSaver.Default.SaveAsync(suggestedFileName, source, cancellationToken);
        if (result.IsSuccessful)
        {
            return ImageSaveResult.Saved;
        }

        return result.Exception switch
        {
            null => ImageSaveResult.Cancelled,
            UnauthorizedAccessException => ImageSaveResult.PermissionDenied,
            _ => ImageSaveResult.Failed,
        };
    }
#endif
}
