using ImageGen.Core.Domain.Descriptions;
using ImageGen.Presentation.Resources.Strings;

namespace ImageGen.Presentation.Services;

public static class DescriptionErrorText
{
    public static string Of(DescriptionError error) => error switch
    {
        DescriptionError.ModelDoesNotAcceptImages => AppResources.DescriptionModelDoesNotAcceptImages,
        DescriptionError.SourceMissing => AppResources.DescriptionSourceMissing,
        DescriptionError.EmptyResponse => AppResources.DescriptionEmptyResponse,
        _ => AppResources.ErrorKindUnknown,
    };
}
