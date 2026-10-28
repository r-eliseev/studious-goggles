using ImageGen.Avalonia.Services.Secrets;

namespace ImageGen.Avalonia.Tests.Services.Secrets;

public class SecretFileNamesTests
{
    [Fact]
    public void PathFor_NameWithSeparator_Throws()
    {
        Assert.Throws<ArgumentException>(() => SecretFileNames.PathFor("/data", "../escape"));
    }

    [Fact]
    public void PathFor_ValidName_IsInSecretsFolderWithBinExtension()
    {
        var path = SecretFileNames.PathFor("/data", "openrouter_api_key");

        Assert.Equal(Path.Combine("/data", "secrets", "openrouter_api_key.bin"), path);
    }
}
