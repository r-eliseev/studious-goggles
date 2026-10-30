using System.Net;
using System.Net.Http.Headers;
using ImageGen.Core.Providers;

namespace ImageGen.Core.Tests.Providers;

public sealed class BoundedHttpContentReaderTests
{
    [Fact]
    public async Task ReadBytesAsync_ContentAtLimit_ReturnsBytes()
    {
        using var content = new ByteArrayContent([1, 2, 3]);

        var result = await BoundedHttpContentReader.ReadBytesAsync(content, 3, TestContext.Current.CancellationToken);

        Assert.Equal([1, 2, 3], result);
    }

    [Fact]
    public async Task ReadBytesAsync_ContentOverLimit_ThrowsSafeSizeException()
    {
        using var content = new ByteArrayContent([1, 2, 3, 4]);

        var exception = await Assert.ThrowsAsync<ProviderResponseTooLargeException>(() =>
            BoundedHttpContentReader.ReadBytesAsync(content, 3, TestContext.Current.CancellationToken));

        Assert.Equal(3, exception.LimitBytes);
        Assert.Equal("Provider response exceeded the configured size limit.", exception.Message);
    }

    [Fact]
    public async Task ReadTextAsync_UsesDeclaredCharset()
    {
        using var content = new ByteArrayContent([0xE9]);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain") { CharSet = "iso-8859-1" };

        var result = await BoundedHttpContentReader.ReadTextAsync(content, 1, TestContext.Current.CancellationToken);

        Assert.Equal("é", result);
    }

    [Fact]
    public async Task ReadBytesAsync_DeclaredLengthOverLimit_DoesNotReadStream()
    {
        using var content = new CountingContent([1, 2, 3, 4]);
        content.Headers.ContentLength = 4;

        await Assert.ThrowsAsync<ProviderResponseTooLargeException>(() =>
            BoundedHttpContentReader.ReadBytesAsync(content, 3, TestContext.Current.CancellationToken));

        Assert.Equal(0, content.BytesRead);
    }

    [Fact]
    public async Task ReadBytesAsync_UnknownLength_ReadsAtMostLimitPlusOne()
    {
        using var content = new CountingContent([1, 2, 3, 4, 5]) { Headers = { ContentLength = null } };

        await Assert.ThrowsAsync<ProviderResponseTooLargeException>(() =>
            BoundedHttpContentReader.ReadBytesAsync(content, 3, TestContext.Current.CancellationToken));

        Assert.Equal(4, content.BytesRead);
    }

    [Fact]
    public async Task ReadBytesAsync_CallerCancellation_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        using var content = new ByteArrayContent([1, 2, 3]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            BoundedHttpContentReader.ReadBytesAsync(content, 3, cancellation.Token));
    }

    private sealed class CountingContent(byte[] bytes) : HttpContent
    {
        public int BytesRead { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => throw new NotSupportedException();

        protected override bool TryComputeLength(out long length)
        {
            length = bytes.Length;
            return true;
        }

        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new CountingStream(bytes, this));

        protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new CountingStream(bytes, this));

        private sealed class CountingStream(byte[] bytes, CountingContent owner) : Stream
        {
            private int _position;

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = Math.Min(Math.Min(buffer.Length, 1), bytes.Length - _position);
                bytes.AsMemory(_position, count).CopyTo(buffer);
                _position += count;
                owner.BytesRead += count;
                return ValueTask.FromResult(count);
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                var read = Math.Min(Math.Min(count, 1), bytes.Length - _position);
                Array.Copy(bytes, _position, buffer, offset, read);
                _position += read;
                owner.BytesRead += read;
                return read;
            }

            public override void Flush() => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
