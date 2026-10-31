using ImageGen.Core.Domain.Keys;
using ImageGen.Core.Platform;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using ImageGen.Presentation.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute.ExceptionExtensions;

namespace ImageGen.Presentation.Tests.PageModels;

public class ApiKeyPageModelTests
{
    private readonly IModelProvider _provider = Substitute.For<IModelProvider>();
    private readonly ISecretStore _secrets = Substitute.For<ISecretStore>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly ISecretPersistence _persistence = Substitute.For<ISecretPersistence>();
    private readonly IProviderRouter _router = Substitute.For<IProviderRouter>();
    private readonly ProviderRegistry _registry = new([
        new ProviderConfig(ProviderIds.OpenRouter, ProviderKind.OpenRouter, "OpenRouter", new Uri("https://openrouter.ai/api/v1/"), TimeSpan.FromSeconds(10), []),
        new ProviderConfig(ProviderIds.Fal, ProviderKind.Fal, "fal.ai", new Uri("https://queue.fal.run/"), TimeSpan.FromSeconds(10), []),
        new ProviderConfig("together", ProviderKind.OpenAiCompatible, "Together AI", new Uri("https://api.together.ai/v1/"), TimeSpan.FromSeconds(10), []),
    ]);

    public ApiKeyPageModelTests() => _router.GetConnection(Arg.Any<string>()).Returns(_provider);

    private ApiKeyPageModel CreateModel(string providerId = ProviderIds.OpenRouter)
    {
        var model = new ApiKeyPageModel(new ApiKeyService(_router, _secrets, NullLogger<ApiKeyService>.Instance), _registry, _navigation, _dialogs, _persistence, ErrorAlerts.Create(_dialogs));
        model.ApplyQueryAttributes(new Dictionary<string, object> { [Routes.ProviderIdParameter] = providerId });
        return model;
    }

    [Theory]
    [InlineData(ProviderIds.OpenRouter, "OpenRouter", SecretNames.OpenRouterApiKey)]
    [InlineData(ProviderIds.Fal, "fal.ai", "provider.fal.api_key")]
    [InlineData("together", "Together AI", "provider.together.api_key")]
    public async Task SaveCommand_ValidProvider_SavesOnlyItsKey(string providerId, string displayName, string secretName)
    {
        _provider.CheckKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ApiKeyStatus.Valid);
        var model = CreateModel(providerId);
        model.ApiKey = "sk-test";

        Assert.Equal(displayName, model.ProviderDisplayName);
        await model.SaveCommand.ExecuteAsync(null);

        await _secrets.Received(1).SetAsync(secretName, "sk-test", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData(null)]
    public async Task OnAppearingAsync_UnknownOrMissingProvider_AlertsAndGoesBackOnceWithoutReadingSecrets(string? providerId)
    {
        var model = new ApiKeyPageModel(new ApiKeyService(_router, _secrets, NullLogger<ApiKeyService>.Instance), _registry, _navigation, _dialogs, _persistence, ErrorAlerts.Create(_dialogs));
        var query = new Dictionary<string, object>();
        if (providerId is not null)
        {
            query[Routes.ProviderIdParameter] = providerId;
        }
        model.ApplyQueryAttributes(query);
        model.ApiKey = "sk-secret";

        await model.OnAppearingAsync();
        await model.OnAppearingAsync();

        Assert.True(model.IsRouteInvalid);
        Assert.False(model.SaveCommand.CanExecute(null));
        await _dialogs.Received(1).AlertAsync(AppResources.ErrorTitle, AppResources.ProviderKeyUnknown);
        await _navigation.Received(1).GoBackAsync();
        await _secrets.DidNotReceive().GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ApplyQueryAttributes_RouteChanges_ClearsOldKeyAndStatus()
    {
        var model = CreateModel();
        model.ApiKey = "sk-old-provider";
        model.StatusText = "old status";

        model.ApplyQueryAttributes(new Dictionary<string, object> { [Routes.ProviderIdParameter] = "together" });

        Assert.Equal("together", model.ProviderId);
        Assert.Equal("Together AI", model.ProviderDisplayName);
        Assert.Equal("", model.ApiKey);
        Assert.Equal("", model.StatusText);
    }

    [Fact]
    public async Task SaveCommand_RouteChangesDuringCheck_UsesCapturedProviderId()
    {
        var check = new TaskCompletionSource<ApiKeyStatus>();
        _provider.CheckKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(check.Task);
        var model = CreateModel();
        model.ApiKey = "key-for-openrouter";

        var saving = model.SaveCommand.ExecuteAsync(null);
        model.ApplyQueryAttributes(new Dictionary<string, object> { [Routes.ProviderIdParameter] = "together" });
        model.ApiKey = "key-for-together";
        check.SetResult(ApiKeyStatus.Valid);
        await saving;

        await _secrets.Received(1).SetAsync(SecretNames.OpenRouterApiKey, "key-for-openrouter", Arg.Any<CancellationToken>());
        await _secrets.DidNotReceive().SetAsync("provider.together.api_key", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnAppearingAsync_NotPersistent_ShowsNotPersistedWarning()
    {
        _persistence.IsPersistentAsync(Arg.Any<CancellationToken>()).Returns(false);
        var model = CreateModel();

        await model.OnAppearingAsync();

        Assert.Equal(AppResources.ApiKeyNotPersisted, model.PersistenceWarning);
        Assert.True(model.HasPersistenceWarning);
    }

    [Fact]
    public async Task OnAppearingAsync_Persistent_NoWarning()
    {
        _persistence.IsPersistentAsync(Arg.Any<CancellationToken>()).Returns(true);
        var model = CreateModel();

        await model.OnAppearingAsync();

        Assert.Equal("", model.PersistenceWarning);
        Assert.False(model.HasPersistenceWarning);
    }

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
    public void HasError_IsFalseBeforeAnyCheck()
    {
        var model = CreateModel();

        Assert.False(model.HasError);
    }

    [Fact]
    public async Task SaveCommand_InvalidKey_ShowsRejectionKeepsFieldAndDoesNotSave()
    {
        _provider.CheckKeyAsync("sk-or-test", Arg.Any<CancellationToken>()).Returns(ApiKeyStatus.Invalid);
        var model = CreateModel();
        model.ApiKey = "sk-or-test";

        await model.SaveCommand.ExecuteAsync(null);

        Assert.Equal(string.Format(AppResources.ProviderKeyInvalidFormat, "OpenRouter"), model.StatusText);
        Assert.True(model.HasError);
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

        Assert.Equal(string.Format(AppResources.ProviderKeyNetworkErrorFormat, "OpenRouter"), model.StatusText);
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

        await _dialogs.Received(1).AlertAsync(AppResources.ErrorTitle, AppResources.ErrorKindUnknown);
        Assert.Equal("", model.StatusText);
        Assert.Equal("sk-or-test", model.ApiKey);
        Assert.False(model.IsBusy);
    }
}
