namespace ImageGen.Core.Services;

public sealed class RandomSeedSource : ISeedSource
{
    /// <summary>Largest seed accepted by all models that support seeds.</summary>
    public const long MaxSeed = int.MaxValue;

    public long NextSeed() => Random.Shared.NextInt64(0, MaxSeed + 1);
}
