using System.Globalization;
using System.Resources;

namespace ImageGen.Presentation.Resources.Strings;

/// <summary>
/// Access to <c>AppResources.resx</c>. Written by hand: Visual Studio code generation does not run for the
/// Windows target in command-line builds. Add a property here for every new string in the .resx file.
/// Format strings (with <c>{0}</c>) are returned as is; the caller applies <c>string.Format</c>.
/// </summary>
public static class AppResources
{
    private static readonly ResourceManager ResourceManager =
        new("ImageGen.Presentation.Resources.Strings.AppResources", typeof(AppResources).Assembly);

    public static string AppTitle => Get(nameof(AppTitle));

    public static string Ok => Get(nameof(Ok));

    public static string Cancel => Get(nameof(Cancel));

    public static string ErrorTitle => Get(nameof(ErrorTitle));

    public static string ApiKeyTitle => Get(nameof(ApiKeyTitle));

    public static string ApiKeyDescription => Get(nameof(ApiKeyDescription));

    public static string ApiKeyPlaceholder => Get(nameof(ApiKeyPlaceholder));

    public static string ApiKeySave => Get(nameof(ApiKeySave));

    public static string ApiKeyChecking => Get(nameof(ApiKeyChecking));

    public static string ApiKeyInvalid => Get(nameof(ApiKeyInvalid));

    public static string ApiKeyNetworkError => Get(nameof(ApiKeyNetworkError));

    public static string ApiKeyNotPersisted => Get(nameof(ApiKeyNotPersisted));

    public static string ComingSoon => Get(nameof(ComingSoon));

    public static string SessionsTitle => Get(nameof(SessionsTitle));

    public static string NewSession => Get(nameof(NewSession));

    public static string DeleteSession => Get(nameof(DeleteSession));

    public static string DeleteSessionTitle => Get(nameof(DeleteSessionTitle));

    public static string DeleteSessionMessage => Get(nameof(DeleteSessionMessage));

    public static string ChatPlaceholder => Get(nameof(ChatPlaceholder));

    public static string Send => Get(nameof(Send));

    public static string AssistantThinking => Get(nameof(AssistantThinking));

    public static string UserRole => Get(nameof(UserRole));

    public static string AssistantRole => Get(nameof(AssistantRole));

    public static string ModelsButtonFormat => Get(nameof(ModelsButtonFormat));

    public static string RunPromptFormat => Get(nameof(RunPromptFormat));

    public static string StatusQueued => Get(nameof(StatusQueued));

    public static string StatusRunning => Get(nameof(StatusRunning));

    public static string StatusSucceeded => Get(nameof(StatusSucceeded));

    public static string StatusFailed => Get(nameof(StatusFailed));

    public static string StatusCancelled => Get(nameof(StatusCancelled));

    public static string StatusFiltered => Get(nameof(StatusFiltered));

    public static string SeedFormat => Get(nameof(SeedFormat));

    public static string SeedNotSupported => Get(nameof(SeedNotSupported));

    public static string CostFormat => Get(nameof(CostFormat));

    public static string Repeat => Get(nameof(Repeat));

    public static string NewSeed => Get(nameof(NewSeed));

    public static string Edit => Get(nameof(Edit));

    public static string EditPromptTitle => Get(nameof(EditPromptTitle));

    public static string EditPromptMessage => Get(nameof(EditPromptMessage));

    public static string Generate => Get(nameof(Generate));

    public static string ModelsTitle => Get(nameof(ModelsTitle));

    public static string ModelsSearchPlaceholder => Get(nameof(ModelsSearchPlaceholder));

    public static string RunsPerModelFormat => Get(nameof(RunsPerModelFormat));

    public static string ModelSupportsSeed => Get(nameof(ModelSupportsSeed));

    public static string ModelNoSeed => Get(nameof(ModelNoSeed));

    public static string Save => Get(nameof(Save));

    public static string Refresh => Get(nameof(Refresh));

    private static string Get(string name) =>
        ResourceManager.GetString(name, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Missing string resource '{name}'.");
}
