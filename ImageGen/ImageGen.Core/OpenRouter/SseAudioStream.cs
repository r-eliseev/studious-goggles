using System.Text;
using System.Text.Json;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Providers;

namespace ImageGen.Core.OpenRouter;

/// <summary>Decodes audio bytes and metadata from an OpenRouter server-sent event stream.</summary>
public sealed class SseAudioStream : Stream, IAudioStreamMetadata
{
    private const int DecodeBufferSize = 48 * 1024;
    private readonly StreamReader _reader;
    private readonly CancellationToken _lifetimeToken;
    private readonly List<byte> _signature = [];
    private byte[] _decoded = [];
    private int _decodedOffset;
    private string? _base64;
    private int _base64Offset;
    private bool _completed;
    private bool _disposed;
    private long _decodedBytes;
    private int _eventCount;
    private int _firstEventAudioBytes;
    private int _lastEventAudioBytes;

    public SseAudioStream(Stream source, CancellationToken lifetimeToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
        {
            throw new ArgumentException("The source stream must be readable.", nameof(source));
        }

        _reader = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: false);
        _lifetimeToken = lifetimeToken;
    }

    public string? MediaType { get; private set; }

    public decimal? CostUsd { get; private set; }

    public string? Transcript { get; private set; }

    public string? UsageJson { get; private set; }

    public int EventCount => _eventCount;

    public int FirstEventAudioBytes => _firstEventAudioBytes;

    public int LastEventAudioBytes => _lastEventAudioBytes;

    public long AudioBytes => _decodedBytes;

    public bool HasTranscript => !string.IsNullOrEmpty(Transcript);

    public override bool CanRead => !_disposed;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (buffer.Length == 0)
        {
            return ValueTask.FromResult(0);
        }

        return ReadCoreAsync(buffer, cancellationToken);
    }

    private async ValueTask<int> ReadCoreAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        using var linked = _lifetimeToken.CanBeCanceled && cancellationToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(_lifetimeToken, cancellationToken)
            : null;
        var token = linked?.Token ?? (_lifetimeToken.CanBeCanceled ? _lifetimeToken : cancellationToken);
        while (true)
        {
            if (_decodedOffset < _decoded.Length)
            {
                var copied = Math.Min(destination.Length, _decoded.Length - _decodedOffset);
                _decoded.AsMemory(_decodedOffset, copied).CopyTo(destination);
                _decodedOffset += copied;
                return copied;
            }

            if (_base64 is not null)
            {
                DecodeNextBase64Chunk();
                if (_decoded.Length > 0)
                {
                    continue;
                }
            }

            if (_completed)
            {
                return 0;
            }

            var line = await _reader.ReadLineAsync(token).ConfigureAwait(false);
            if (line is null)
            {
                Complete();
                continue;
            }

            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var payload = line.AsSpan("data:".Length).Trim();
            if (payload.IsEmpty)
            {
                continue;
            }

            if (payload.SequenceEqual("[DONE]"))
            {
                Complete();
                continue;
            }

            ReadEvent(payload.ToString());
        }
    }

    private void ReadEvent(string payload)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException exception)
        {
            throw new ProviderException(ErrorKind.Unknown, "OpenRouter returned invalid audio stream data.", innerException: exception);
        }

        using (document)
        {
            ReadEvent(document.RootElement);
        }
    }

    private void ReadEvent(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ProviderException(ErrorKind.Unknown, "OpenRouter returned invalid audio stream data.");
        }

        _eventCount++;
        var eventAudioBytes = 0;
        if (root.TryGetProperty("error", out var error))
        {
            var message = error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var detail)
                ? detail.GetString()
                : error.ToString();
            throw new ProviderException(ErrorKind.Unknown, string.IsNullOrWhiteSpace(message) ? "OpenRouter audio stream failed." : message);
        }

        if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0
            && choices[0].ValueKind == JsonValueKind.Object
            && choices[0].TryGetProperty("delta", out var delta) && delta.ValueKind == JsonValueKind.Object
            && delta.TryGetProperty("audio", out var audio) && audio.ValueKind == JsonValueKind.Object)
        {
            if (audio.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.String)
            {
                _base64 = data.GetString();
                _base64Offset = 0;
                if (!string.IsNullOrEmpty(_base64))
                {
                    var padding = _base64.EndsWith("==", StringComparison.Ordinal) ? 2
                        : _base64.EndsWith('=') ? 1
                        : 0;
                    eventAudioBytes = (_base64.Length / 4 * 3) - padding;
                }
            }

            if (audio.TryGetProperty("transcript", out var transcript) && transcript.ValueKind == JsonValueKind.String)
            {
                Transcript = string.Concat(Transcript, transcript.GetString());
            }
        }

        if (root.TryGetProperty("usage", out var usage)
            && usage.ValueKind == JsonValueKind.Object)
        {
            UsageJson = usage.GetRawText();
            if (usage.TryGetProperty("cost", out var cost) && cost.TryGetDecimal(out var parsedCost))
            {
                CostUsd = parsedCost;
            }
        }

        if (_eventCount == 1)
        {
            _firstEventAudioBytes = eventAudioBytes;
        }

        _lastEventAudioBytes = eventAudioBytes;
    }

    private void DecodeNextBase64Chunk()
    {
        var encoded = _base64!;
        if (_base64Offset >= encoded.Length)
        {
            _base64 = null;
            _decoded = [];
            _decodedOffset = 0;
            return;
        }

        var charsToDecode = Math.Min(DecodeBufferSize, encoded.Length - _base64Offset);
        charsToDecode -= charsToDecode % 4;
        if (charsToDecode == 0)
        {
            throw new ProviderException(ErrorKind.Unknown, "OpenRouter returned invalid base64 audio data.");
        }

        var output = new byte[charsToDecode / 4 * 3];
        if (!Convert.TryFromBase64Chars(encoded.AsSpan(_base64Offset, charsToDecode), output, out var bytesWritten))
        {
            throw new ProviderException(ErrorKind.Unknown, "OpenRouter returned invalid base64 audio data.");
        }

        _base64Offset += charsToDecode;
        if (_base64Offset >= encoded.Length)
        {
            _base64 = null;
            _base64Offset = 0;
        }

        _decodedBytes += bytesWritten;
        if (_decodedBytes > ProviderResponseLimits.Audio)
        {
            throw new ProviderException(
                ErrorKind.Unknown,
                "OpenRouter audio exceeded the configured size limit.",
                innerException: new ProviderResponseTooLargeException(ProviderResponseLimits.Audio, _decodedBytes));
        }

        var bytes = output.AsSpan(0, bytesWritten).ToArray();
        if (MediaType is null)
        {
            for (var index = 0; index < bytes.Length; index++)
            {
                _signature.Add(bytes[index]);
                MediaType = DetectMediaType(_signature, endOfStream: false);
                if (MediaType is not null)
                {
                    _decoded = _signature.ToArray();
                    _decodedOffset = 0;
                    _signature.Clear();
                    if (index + 1 < bytes.Length)
                    {
                        _decoded = [.. _decoded, .. bytes.AsSpan(index + 1).ToArray()];
                    }

                    break;
                }
            }
        }
        else
        {
            _decoded = bytes;
            _decodedOffset = 0;
        }
    }

    private static string? DetectMediaType(IReadOnlyList<byte> signature, bool endOfStream)
    {
        if (signature.Count >= 3 && signature[0] == (byte)'I' && signature[1] == (byte)'D' && signature[2] == (byte)'3')
        {
            return "audio/mpeg";
        }

        if (signature.Count >= 2 && signature[0] == 0xFF && (signature[1] & 0xE0) == 0xE0)
        {
            return "audio/mpeg";
        }

        if (signature.Count >= 4 && signature[0] == (byte)'O' && signature[1] == (byte)'g' && signature[2] == (byte)'g' && signature[3] == (byte)'S')
        {
            return "audio/ogg";
        }

        if (signature.Count >= 4 && signature[0] == (byte)'f' && signature[1] == (byte)'L' && signature[2] == (byte)'a' && signature[3] == (byte)'C')
        {
            return "audio/flac";
        }

        if (signature.Count >= 12
            && signature.Take(4).SequenceEqual("RIFF"u8.ToArray())
            && signature.Skip(8).Take(4).SequenceEqual("WAVE"u8.ToArray()))
        {
            return "audio/wav";
        }

        if (signature.Count >= 12 || endOfStream)
        {
            throw new ProviderException(ErrorKind.Unknown, "OpenRouter returned an unknown audio format.");
        }

        return null;
    }

    private void Complete()
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        if (_decodedBytes == 0)
        {
            throw new ProviderException(ErrorKind.EmptyResponse, "audio stream contained no audio");
        }

        if (MediaType is null)
        {
            MediaType = DetectMediaType(_signature, endOfStream: true);
            _decoded = _signature.ToArray();
            _decodedOffset = 0;
            _signature.Clear();
        }
    }

    public override void Flush() => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _reader.Dispose();
            _disposed = true;
        }

        base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
