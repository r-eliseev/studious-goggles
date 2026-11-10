namespace ImageGen.Core.Providers;

public sealed class AudioGenerationResponse(Stream content, string? mediaType, IDisposable? owner = null) : IAsyncDisposable
{
    private readonly IDisposable? _owner = owner;

    public Stream Content { get; } = content ?? throw new ArgumentNullException(nameof(content));

    public string MediaType
    {
        get
        {
            if (Content is not IAudioStreamMetadata metadata)
            {
                return mediaType ?? "application/octet-stream";
            }

            return metadata.MediaType is { } detected && detected != "application/octet-stream"
                ? detected
                : mediaType ?? metadata.MediaType ?? "application/octet-stream";
        }
    }

    public decimal? CostUsd => Content is IAudioStreamMetadata metadata ? metadata.CostUsd : null;

    public string? Transcript => Content is IAudioStreamMetadata metadata ? metadata.Transcript : null;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Content.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _owner?.Dispose();
        }
    }
}

internal interface IAudioStreamMetadata
{
    string? MediaType { get; }

    decimal? CostUsd { get; }

    string? Transcript { get; }
}

internal sealed class AudioFormatDetectingStream(Stream source, CancellationToken lifetimeToken = default) : Stream, IAudioStreamMetadata
{
    private readonly Stream _source = source ?? throw new ArgumentNullException(nameof(source));
    private readonly byte[] _prefix = new byte[12];
    private int _prefixLength;
    private int _prefixOffset;
    private bool _initialized;
    private bool _disposed;
    private readonly CancellationToken _lifetimeToken = lifetimeToken;
    private long _bytesRead;

    public string? MediaType { get; private set; }

    public decimal? CostUsd => null;

    public string? Transcript => null;

    public override bool CanRead => !_disposed;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return ReadCoreAsync(buffer, cancellationToken);
    }

    private async ValueTask<int> ReadCoreAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (buffer.IsEmpty)
        {
            return 0;
        }

        using var linked = _lifetimeToken.CanBeCanceled && cancellationToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(_lifetimeToken, cancellationToken)
            : null;
        var token = linked?.Token ?? (_lifetimeToken.CanBeCanceled ? _lifetimeToken : cancellationToken);
        await EnsureInitializedAsync(token).ConfigureAwait(false);
        if (_prefixOffset < _prefixLength)
        {
            var count = Math.Min(buffer.Length, _prefixLength - _prefixOffset);
            _prefix.AsMemory(_prefixOffset, count).CopyTo(buffer);
            _prefixOffset += count;
            return count;
        }

        var read = await _source.ReadAsync(buffer, token).ConfigureAwait(false);
        CheckSize(read);
        return read;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        while (_prefixLength < _prefix.Length && MediaType is null)
        {
            var read = await _source.ReadAsync(_prefix.AsMemory(_prefixLength), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            _prefixLength += read;
            CheckSize(read);
            MediaType = DetectMediaType(_prefix.AsSpan(0, _prefixLength));
        }

        MediaType ??= DetectMediaType(_prefix.AsSpan(0, _prefixLength)) ?? "application/octet-stream";
    }

    private void CheckSize(int count)
    {
        _bytesRead += count;
        if (_bytesRead > ProviderResponseLimits.Audio)
        {
            throw new ProviderException(
                ImageGen.Core.Domain.Generations.ErrorKind.Unknown,
                "OpenRouter audio exceeded the configured size limit.",
                innerException: new ProviderResponseTooLargeException(ProviderResponseLimits.Audio, _bytesRead));
        }
    }

    private static string? DetectMediaType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith("ID3"u8) || (bytes.Length >= 2 && bytes[0] == 0xFF && (bytes[1] & 0xE0) == 0xE0))
        {
            return "audio/mpeg";
        }

        if (bytes.StartsWith("OggS"u8))
        {
            return "audio/ogg";
        }

        if (bytes.StartsWith("fLaC"u8))
        {
            return "audio/flac";
        }

        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WAVE"u8))
        {
            return "audio/wav";
        }

        return null;
    }

    public override void Flush() => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _source.Dispose();
            _disposed = true;
        }

        base.Dispose(disposing);
    }
}
