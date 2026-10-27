namespace ImageGen.Core.Services;

public interface ISeedSource
{
    /// <summary>Returns a seed from 0 to 2147483647 inclusive.</summary>
    long NextSeed();
}
