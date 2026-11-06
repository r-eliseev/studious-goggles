using Android.Content;
using Android.Provider;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;

namespace ImageGen.Avalonia.Platform;

/// <summary>Saves generated images to the shared Pictures/Omnilite gallery through MediaStore.</summary>
public sealed class AndroidImageSaver : IImageSaver
{
    private const string GalleryFolder = "Pictures/Omnilite";

    public string ActionLabel => AppResources.SaveToGallery;

    public async Task<ImageSaveResult> SaveAsync(
        string sourcePath,
        string suggestedFileName,
        string mediaType,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            return ImageSaveResult.Failed;
        }

        var resolver = global::Android.App.Application.Context.ContentResolver;
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
            using var target = resolver.OpenOutputStream(uri) ?? throw new IOException("MediaStore did not open the image.");
            await using var source = File.OpenRead(sourcePath);
            await source.CopyToAsync(target, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            resolver.Delete(uri, null, null);
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            resolver.Delete(uri, null, null);
            return ImageSaveResult.PermissionDenied;
        }
        catch (IOException)
        {
            resolver.Delete(uri, null, null);
            return ImageSaveResult.Failed;
        }

        var published = new ContentValues();
        published.Put(MediaStore.IMediaColumns.IsPending, 0);
        resolver.Update(uri, published, null, null);
        return ImageSaveResult.Saved;
    }
}
