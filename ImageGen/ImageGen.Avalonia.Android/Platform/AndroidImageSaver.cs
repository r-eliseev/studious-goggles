using Android.Content;
using Android.Provider;
using ImageGen.Avalonia.Services;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using Microsoft.Extensions.Logging;

namespace ImageGen.Avalonia.Platform;

/// <summary>Saves generated images to the shared Pictures/Omnilite gallery through MediaStore.</summary>
public sealed class AndroidImageSaver : IImageSaver
{
    private const string GalleryFolder = "Pictures/Omnilite";

    private readonly ILogger<AndroidImageSaver> _logger;
    private readonly AvaloniaImageSaver _fileSaver;

    public AndroidImageSaver(ILogger<AndroidImageSaver> logger, AvaloniaImageSaver fileSaver)
    {
        _logger = logger;
        _fileSaver = fileSaver;
    }

    public string ActionLabel => OperatingSystem.IsAndroidVersionAtLeast(29) ? AppResources.SaveToGallery : _fileSaver.ActionLabel;

    public async Task<ImageSaveResult> SaveAsync(
        string sourcePath,
        string suggestedFileName,
        string mediaType,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            return await _fileSaver.SaveAsync(sourcePath, suggestedFileName, mediaType, cancellationToken);
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

            var published = new ContentValues();
            published.Put(MediaStore.IMediaColumns.IsPending, 0);
            resolver.Update(uri, published, null, null);
            return ImageSaveResult.Saved;
        }
        catch (OperationCanceledException)
        {
            DeletePending(resolver, uri);
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            DeletePending(resolver, uri);
            return ImageSaveResult.PermissionDenied;
        }
        catch (IOException)
        {
            DeletePending(resolver, uri);
            return ImageSaveResult.Failed;
        }
        catch (Java.Lang.Exception exception)
        {
            DeletePending(resolver, uri);
            _logger.LogWarning("Saving image to MediaStore failed with {ExceptionType}.", exception.GetType().Name);
            return ImageSaveResult.Failed;
        }
        catch
        {
            DeletePending(resolver, uri);
            throw;
        }
    }

    private static void DeletePending(ContentResolver resolver, global::Android.Net.Uri uri)
    {
        try
        {
            resolver.Delete(uri, null, null);
        }
        catch (Exception)
        {
            // Preserve the original cancellation or save failure if cleanup itself fails.
        }
    }
}
