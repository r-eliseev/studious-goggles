using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Maui.PageModels;
using ImageGen.Maui.Resources.Strings;
using ImageGen.Maui.Services;
using NSubstitute.ExceptionExtensions;

namespace ImageGen.Core.Tests.MauiLinked;

public class ApiKeyPageModelTests
{
    private readonly IModelProvider _provider = Substitute.For<IModelProvider>();
    private readonly ISecretStore _secrets = Substitute.For<ISecretStore>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    private ApiKeyPageModel CreateModel() => new(new ApiKeyService(_secrets, _provider), _navigation, _dialogs);

    [Fact]
    public void SaveCommand_EmptyKey_CannotExecute()
    {
        var model = CreateModel();

        Assert.False(model.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void SaveCommand_WhitespaceKey_CannotExecute()
    {
        var model = CreateModel();
        model.ApiKey = "   ";

        Assert.False(model.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void SaveCommand_KeyEntered_CanExecute()
    {
        var model = CreateModel();
        model.ApiKey = "sk-or-test";

        Assert.True(model.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task SaveCommand_ValidKey_SavesKeyClearsFieldAndGoesBack()
    {
        _provider.CheckKeyAsync("sk-or-test", Arg.Any<CancellationToken>()).Returns(ApiKeyStatus.Valid);
        var model = CreateModel();
        model.ApiKey = "  sk-or-test  ";

        await model.SaveCommand.ExecuteAsync(null);

        await _secrets.Received(1).SetAsync(SecretNames.OpenRouterApiKey, "sk-or-test", Arg.Any<CancellationToken>());
        Assert.Equal("", model.ApiKey);
        Assert.Equal("", model.StatusText);
        await _navigation.Received(1).GoBackAsync();
        Assert.False(model.IsBusy);
    }

    [Fact]
    public async Task SaveCommand_InvalidKey_ShowsRejectionKeepsFieldAndDoesNotSave()
    {
        _provider.CheckKeyAsync("sk-or-test", Arg.Any<CancellationToken>()).Returns(ApiKeyStatus.Invalid);
        var model = CreateModel();
        model.ApiKey = "sk-or-test";

        await model.SaveCommand.ExecuteAsync(null);

        Assert.Equal(AppResources.ApiKeyInvalid, model.StatusText);
        Assert.Equal("sk-or-test", model.ApiKey);
        Assert.False(model.IsBusy);
        await _secrets.DidNotReceive().SetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _navigation.DidNotReceive().GoBackAsync();
    }

    [Fact]
    public async Task SaveCommand_NetworkError_ShowsConnectionMessageKeepsFieldAndDoesNotSave()
    {
        _provider.CheckKeyAsync("sk-or-test", Arg.Any<CancellationToken>()).Returns(ApiKeyStatus.NetworkError);
        var model = CreateModel();
        model.ApiKey = "sk-or-test";

        await model.SaveCommand.ExecuteAsync(null);

        Assert.Equal(AppResources.ApiKeyNetworkError, model.StatusText);
        Assert.Equal("sk-or-test", model.ApiKey);
        Assert.False(model.IsBusy);
        await _secrets.DidNotReceive().SetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _navigation.DidNotReceive().GoBackAsync();
    }

    [Fact]
    public async Task SaveCommand_WhileChecking_IsBusyShowsCheckingAndCannotStartAgain()
    {
        var check = new TaskCompletionSource<ApiKeyStatus>();
        _provider.CheckKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(check.Task);
        var model = CreateModel();
        model.ApiKey = "sk-or-test";

        var running = model.SaveCommand.ExecuteAsync(null);

        Assert.True(model.IsBusy);
        Assert.Equal(AppResources.ApiKeyChecking, model.StatusText);
        Assert.False(model.SaveCommand.CanExecute(null));

        check.SetResult(ApiKeyStatus.Invalid);
        await running;

        Assert.False(model.IsBusy);
        Assert.True(model.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task SaveCommand_UnexpectedError_ShowsAlertClearsStatusAndKeepsField()
    {
        _provider.CheckKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("disk is full"));
        var model = CreateModel();
        model.ApiKey = "sk-or-test";

        await model.SaveCommand.ExecuteAsync(null);

        await _dialogs.Received(1).AlertAsync(AppResources.ErrorTitle, "disk is full");
        Assert.Equal("", model.StatusText);
        Assert.Equal("sk-or-test", model.ApiKey);
        Assert.False(model.IsBusy);
    }
}
