using System.Collections.Concurrent;
using ImageGen.Core.Services;

namespace ImageGen.Acceptance.Tests.Support;

public sealed class SequenceSeedSource : ISeedSource
{
    private readonly ConcurrentQueue<long> _seeds = new();
    private long _nextSeed = 1;

    public SequenceSeedSource(params long[] seeds) => SetSequence(seeds);

    public void SetSequence(IEnumerable<long> seeds)
    {
        ArgumentNullException.ThrowIfNull(seeds);
        while (_seeds.TryDequeue(out _)) { }
        var sequence = seeds.ToArray();
        foreach (var seed in sequence)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(seed);
            if (seed > RandomSeedSource.MaxSeed) throw new ArgumentOutOfRangeException(nameof(seeds));
            _seeds.Enqueue(seed);
        }

        Interlocked.Exchange(ref _nextSeed, sequence.Length == 0 ? 1 : checked(sequence[^1] + 1));
    }

    public long NextSeed() => _seeds.TryDequeue(out var seed)
        ? seed
        : Interlocked.Increment(ref _nextSeed) - 1;
}
