namespace ImageGen.Core.Services;

/// <summary>Source of generation seeds. Replaceable in tests.</summary>
public interface ISeedSource
{
    /// <summary>Returns a seed from 0 to <see cref="RandomSeedSource.MaxSeed"/>, inclusive.</summary>
    long NextSeed();
}
