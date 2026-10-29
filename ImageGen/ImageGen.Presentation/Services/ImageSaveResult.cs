namespace ImageGen.Presentation.Services;

/// <summary>Outcome of saving a picture: the user picked a place, cancelled, lacks permission, or the write failed.</summary>
public enum ImageSaveResult
{
    Saved,
    Cancelled,
    PermissionDenied,
    Failed,
}
