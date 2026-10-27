namespace ImageGen.Core.Services;

public sealed class RandomSeedSource : ISeedSource
{
    public const long MaxSeed = int.MaxValue;

    public long NextSeed() => Random.Shared.NextInt64(0, MaxSeed + 1);
}
