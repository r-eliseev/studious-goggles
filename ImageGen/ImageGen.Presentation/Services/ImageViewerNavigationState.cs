namespace ImageGen.Presentation.Services;

/// <summary>Tracks requested and displayed viewer positions and rejects stale image load completions.</summary>
public sealed class ImageViewerNavigationState
{
    private readonly object _gate = new();
    private readonly int _count;
    private int _requestedIndex;
    private int? _displayedIndex;
    private int _epoch;
    private bool _closed;

    public ImageViewerNavigationState(int count, int initialIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        if ((uint)initialIndex >= (uint)count) throw new ArgumentOutOfRangeException(nameof(initialIndex));
        _count = count;
        _requestedIndex = initialIndex;
    }

    public int? DisplayedIndex
    {
        get { lock (_gate) return _displayedIndex; }
    }

    public (int Index, int Epoch) InitialRequest
    {
        get { lock (_gate) return (_requestedIndex, _epoch); }
    }

    public bool TryNavigate(int delta, out (int Index, int Epoch) request)
    {
        if (delta is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(delta));
        lock (_gate)
        {
            var next = _requestedIndex + delta;
            if (_closed || (uint)next >= (uint)_count)
            {
                request = default;
                return false;
            }

            _requestedIndex = next;
            request = (next, ++_epoch);
            return true;
        }
    }

    public bool TryCommit((int Index, int Epoch) request)
    {
        lock (_gate)
        {
            if (!IsCurrent(request)) return false;
            _displayedIndex = request.Index;
            return true;
        }
    }

    /// <summary>Invalidates the failed request and restores the last displayed position.</summary>
    public bool TryFail((int Index, int Epoch) request, out int failureEpoch, out int? displayedIndex)
    {
        lock (_gate)
        {
            if (!IsCurrent(request))
            {
                failureEpoch = _epoch;
                displayedIndex = _displayedIndex;
                return false;
            }

            _requestedIndex = _displayedIndex ?? request.Index;
            failureEpoch = ++_epoch;
            displayedIndex = _displayedIndex;
            return true;
        }
    }

    public bool IsCurrentEpoch(int epoch)
    {
        lock (_gate) return !_closed && _epoch == epoch;
    }

    public void Close()
    {
        lock (_gate)
        {
            _closed = true;
            _epoch++;
        }
    }

    private bool IsCurrent((int Index, int Epoch) request) =>
        !_closed && request.Epoch == _epoch && request.Index == _requestedIndex;
}
