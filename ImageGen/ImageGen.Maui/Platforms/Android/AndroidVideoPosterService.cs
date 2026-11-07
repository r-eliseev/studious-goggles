using Android.Graphics;
using Android.Media;
using ImageGen.Presentation.Services;
using Microsoft.Extensions.Logging;

namespace ImageGen.Maui.Platforms.Android;

/// <summary>Extracts a small JPEG poster from a local video and caches it beside the source.</summary>
public sealed class AndroidVideoPosterService(ILogger<AndroidVideoPosterService> logger) : IVideoPosterService
{
    private const int MaxWidth = 528;

    public bool IsSupported => true;

    public Task<PosterInfo?> GetPosterAsync(string videoPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoPath);
        return Task.Run(() => Extract(videoPath, cancellationToken), cancellationToken);
    }

    private PosterInfo? Extract(string videoPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var posterPath = videoPath + ".poster.jpg";
        string? temporaryPath = null;
        try
        {
            if (TryReadExisting(posterPath) is { } existing)
            {
                return existing;
            }

            using var retriever = new MediaMetadataRetriever();
            retriever.SetDataSource(videoPath);
            using var frame = retriever.GetFrameAtTime(0, Option.ClosestSync);
            if (frame is null)
            {
                logger.LogWarning("Video poster extraction failed ({FailureType}).", "NoFrame");
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var width = Math.Min(frame.Width, MaxWidth);
            var height = Math.Max(1, (int)Math.Round(frame.Height * (double)width / frame.Width));
            using var resized = width == frame.Width ? null : Bitmap.CreateScaledBitmap(frame, width, height, true);
            var output = resized ?? frame;
            temporaryPath = posterPath + ".tmp-" + Guid.NewGuid().ToString("N");
            using (var stream = File.Create(temporaryPath))
            {
                if (!output.Compress(Bitmap.CompressFormat.Jpeg!, 80, stream))
                {
                    throw new IOException("JPEG compression failed.");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, posterPath, overwrite: true);
            temporaryPath = null;
            return new PosterInfo(posterPath, output.Width, output.Height);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Video poster extraction failed ({FailureType}).", ex.GetType().Name);
            return null;
        }
        finally
        {
            if (temporaryPath is not null && File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static PosterInfo? TryReadExisting(string path)
    {
        if (!File.Exists(path)) return null;
        using var bitmap = BitmapFactory.DecodeFile(path);
        return bitmap is null ? null : new PosterInfo(path, bitmap.Width, bitmap.Height);
    }
}
