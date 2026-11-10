namespace ImageGen.Core.Providers;

public interface IAudioGenerationProvider
{
    Task<AudioGenerationResponse> GenerateAudioAsync(AudioGenerationRequest request, CancellationToken cancellationToken);
}
