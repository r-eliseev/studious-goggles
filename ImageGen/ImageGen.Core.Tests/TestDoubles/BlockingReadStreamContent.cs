using System.Net;

namespace ImageGen.Core.Tests.TestDoubles;

/// <summary>Content whose read blocks until its caller cancellation token is cancelled.</summary>
internal sealed class BlockingReadStreamContent(TaskCompletionSource readStarted) : HttpContent
{
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => throw new NotSupportedException();

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) => throw new NotSupportedException();

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }

    protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new BlockingReadStream(readStarted));

    protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken) =>
        Task.FromResult<Stream>(new BlockingReadStream(readStarted, cancellationToken));

    private sealed class BlockingReadStream(TaskCompletionSource readStarted, CancellationToken initialToken = default) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            readStarted.TrySetResult();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(initialToken, cancellationToken);
            await Task.Delay(Timeout.Infinite, linked.Token);
            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
