using System.Net;

namespace ImageGen.Core.Tests.TestDoubles;

/// <summary>A non-seekable generated content stream with optional declared length and disposal counters.</summary>
internal sealed class CountingStreamContent(long actualLength, long? declaredLength = null, byte value = (byte)'x') : HttpContent
{
    private CountingReadStream? _stream;

    public long BytesRead => _stream?.BytesRead ?? 0;

    public bool StreamDisposed => _stream?.IsDisposed ?? false;

    public bool ContentDisposed { get; private set; }

    public CountingStreamContent SetContentLength(long? length)
    {
        Headers.ContentLength = length;
        return this;
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => SerializeAsync(stream, CancellationToken.None);

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) => SerializeAsync(stream, cancellationToken);

    protected override bool TryComputeLength(out long length)
    {
        if (declaredLength is { } known)
        {
            length = known;
            return true;
        }
        length = 0;
        return false;
    }

    protected override Task<Stream> CreateContentReadStreamAsync() => CreateStream();

    protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken) => CreateStream();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ContentDisposed = true;
            _stream?.Dispose();
        }
        base.Dispose(disposing);
    }

    private Task<Stream> CreateStream()
    {
        _stream ??= new CountingReadStream(actualLength, value);
        return Task.FromResult<Stream>(_stream);
    }

    private async Task SerializeAsync(Stream destination, CancellationToken cancellationToken)
    {
        await using var stream = await CreateStream();
        var buffer = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0) return;
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private sealed class CountingReadStream(long length, byte value) : Stream
    {
        private long _position;

        public long BytesRead => _position;
        public bool IsDisposed { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = (int)Math.Min(Math.Min(buffer.Length, 8192), length - _position);
            buffer.Span[..count].Fill(value);
            _position += count;
            return ValueTask.FromResult(count);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = (int)Math.Min(Math.Min(count, 8192), length - _position);
            buffer.AsSpan(offset, read).Fill(value);
            _position += read;
            return read;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) IsDisposed = true;
            base.Dispose(disposing);
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
