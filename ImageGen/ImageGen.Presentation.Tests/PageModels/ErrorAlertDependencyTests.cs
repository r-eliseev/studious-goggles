using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Services;

namespace ImageGen.Presentation.Tests.PageModels;

public sealed class ErrorAlertDependencyTests
{
    [Theory]
    [InlineData(typeof(ApiKeyPageModel))]
    [InlineData(typeof(ChatPageModel))]
    [InlineData(typeof(ModelPickerPageModel))]
    [InlineData(typeof(SessionModelsPageModel))]
    [InlineData(typeof(SessionParametersPageModel))]
    [InlineData(typeof(SessionsPageModel))]
    [InlineData(typeof(SettingsPageModel))]
    [InlineData(typeof(ProviderKeyItemViewModel))]
    public void Constructor_ErrorAlertParameterIsRequired(Type pageModelType)
    {
        var parameter = Assert.Single(pageModelType.GetConstructors().SelectMany(constructor => constructor.GetParameters()),
            parameter => parameter.ParameterType == typeof(ErrorAlert));

        Assert.False(parameter.HasDefaultValue);
    }
}
