namespace ImageGen.Core.Storage;

public sealed class StorageWriteException(string message, bool isTransient, Exception? innerException = null)
    : Exception(message, innerException)
{
    public bool IsTransient { get; } = isTransient;
}
