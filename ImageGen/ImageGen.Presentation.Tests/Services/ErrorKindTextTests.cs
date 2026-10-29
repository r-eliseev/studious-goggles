using ImageGen.Core.Domain.Generations;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;

namespace ImageGen.Presentation.Tests.Services;

public class ErrorKindTextTests
{
    [Fact]
    public void Of_EveryErrorKind_HasItsOwnText()
    {
        var texts = Enum.GetValues<ErrorKind>().Select(ErrorKindText.Of).ToList();

        Assert.All(texts, text => Assert.False(string.IsNullOrWhiteSpace(text)));
        Assert.Equal(texts.Count, texts.Distinct().Count());
    }

    [Fact]
    public void Of_UnknownCode_ShowsTheUnknownErrorText()
    {
        Assert.Equal(AppResources.ErrorKindUnknown, ErrorKindText.Of((ErrorKind)999));
    }

    [Fact]
    public void Of_RateLimited_ShowsTheCodeTextNotTheProviderText()
    {
        Assert.Equal(AppResources.ErrorKindRateLimited, ErrorKindText.Of(ErrorKind.RateLimited));
    }
}
