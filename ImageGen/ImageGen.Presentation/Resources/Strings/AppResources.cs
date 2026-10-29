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

    public static string FilteredNotice => Get(nameof(FilteredNotice));

    public static string TimeFormat => Get(nameof(TimeFormat));

    public static string RunHeaderFormat => Get(nameof(RunHeaderFormat));

    public static string SeedFormat => Get(nameof(SeedFormat));

    public static string SeedNotSupported => Get(nameof(SeedNotSupported));

    public static string CostFormat => Get(nameof(CostFormat));

    public static string Repeat => Get(nameof(Repeat));

    public static string Copy => Get(nameof(Copy));

    public static string Copied => Get(nameof(Copied));

    public static string ImageModelsSelectedFormat => Get(nameof(ImageModelsSelectedFormat));

    public static string SettingsTitle => Get(nameof(SettingsTitle));

    public static string SettingsOpenRouter => Get(nameof(SettingsOpenRouter));

    public static string SettingsApiKey => Get(nameof(SettingsApiKey));

    public static string SettingsKeyConnectedFormat => Get(nameof(SettingsKeyConnectedFormat));

    public static string SettingsKeyMissing => Get(nameof(SettingsKeyMissing));

    public static string SettingsChangeKey => Get(nameof(SettingsChangeKey));

    public static string SettingsAppearance => Get(nameof(SettingsAppearance));

    public static string SettingsStyle => Get(nameof(SettingsStyle));

    public static string SettingsTheme => Get(nameof(SettingsTheme));

    public static string StyleQuiet => Get(nameof(StyleQuiet));

    public static string StyleQuietHint => Get(nameof(StyleQuietHint));

    public static string StyleGallery => Get(nameof(StyleGallery));

    public static string StyleGalleryHint => Get(nameof(StyleGalleryHint));

    public static string StyleNotebook => Get(nameof(StyleNotebook));

    public static string StyleNotebookHint => Get(nameof(StyleNotebookHint));

    public static string ThemeSystem => Get(nameof(ThemeSystem));

    public static string ThemeLight => Get(nameof(ThemeLight));

    public static string ThemeDark => Get(nameof(ThemeDark));

    public static string SettingsAbout => Get(nameof(SettingsAbout));

    public static string SettingsLogs => Get(nameof(SettingsLogs));

    public static string SettingsOpenLogs => Get(nameof(SettingsOpenLogs));

    public static string SessionsToday => Get(nameof(SessionsToday));

    public static string SessionsEarlier => Get(nameof(SessionsEarlier));

    public static string EmptyChatTitle => Get(nameof(EmptyChatTitle));

    public static string EmptyChatHint => Get(nameof(EmptyChatHint));

    public static string ApiKeyHeading => Get(nameof(ApiKeyHeading));

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

    public static string AssistantModelTitle => Get(nameof(AssistantModelTitle));

    public static string AssistantLabelFormat => Get(nameof(AssistantLabelFormat));

    public static string AssistantPriceFormat => Get(nameof(AssistantPriceFormat));

    public static string AssistantPriceUnknown => Get(nameof(AssistantPriceUnknown));

    public static string AssistantFree => Get(nameof(AssistantFree));

    public static string AssistantTakesImages => Get(nameof(AssistantTakesImages));

    public static string AssistantSelected => Get(nameof(AssistantSelected));

    public static string SessionParametersTitle => Get(nameof(SessionParametersTitle));

    public static string SessionParametersDescription => Get(nameof(SessionParametersDescription));

    public static string ParameterModelDefault => Get(nameof(ParameterModelDefault));

    public static string ParameterAspectRatio => Get(nameof(ParameterAspectRatio));

    public static string ParameterResolution => Get(nameof(ParameterResolution));

    public static string ParameterOutputFormat => Get(nameof(ParameterOutputFormat));

    public static string ParameterQuality => Get(nameof(ParameterQuality));

    public static string ParameterSave => Get(nameof(ParameterSave));

    public static string ParameterReplacedFormat => Get(nameof(ParameterReplacedFormat));

    public static string ParameterDroppedFormat => Get(nameof(ParameterDroppedFormat));

    public static string ReasonNotSupported => Get(nameof(ReasonNotSupported));

    public static string ReasonNoAllowedValue => Get(nameof(ReasonNoAllowedValue));

    public static string ReasonCatalogUnknown => Get(nameof(ReasonCatalogUnknown));

    public static string ChatParametersButton => Get(nameof(ChatParametersButton));

    public static string AttachImageButton => Get(nameof(AttachImageButton));

    public static string AttachmentsCountFormat => Get(nameof(AttachmentsCountFormat));

    public static string AttachmentLimitMessage => Get(nameof(AttachmentLimitMessage));

    public static string ImageConsentTitle => Get(nameof(ImageConsentTitle));

    public static string ImageConsentMessage => Get(nameof(ImageConsentMessage));

    public static string ImageConsentAccept => Get(nameof(ImageConsentAccept));

    public static string ReferenceFormat => Get(nameof(ReferenceFormat));

    public static string RemoveAttachment => Get(nameof(RemoveAttachment));

    public static string DescribeButton => Get(nameof(DescribeButton));

    public static string DescribePromptButton => Get(nameof(DescribePromptButton));

    public static string More => Get(nameof(More));

    public static string ActionEditPrompt => Get(nameof(ActionEditPrompt));

    public static string ActionDescribeImage => Get(nameof(ActionDescribeImage));

    public static string ActionPromptFromImage => Get(nameof(ActionPromptFromImage));

    public static string DescriptionTitle => Get(nameof(DescriptionTitle));

    public static string DescriptionCostUnknown => Get(nameof(DescriptionCostUnknown));

    public static string DescriptionFromFormat => Get(nameof(DescriptionFromFormat));

    public static string DescriptionInsert => Get(nameof(DescriptionInsert));

    public static string DescriptionClose => Get(nameof(DescriptionClose));

    public static string DescriptionReplace => Get(nameof(DescriptionReplace));

    public static string DescriptionAppend => Get(nameof(DescriptionAppend));

    public static string DescriptionReplaceQuestion => Get(nameof(DescriptionReplaceQuestion));

    public static string ErrorDetailsTitle => Get(nameof(ErrorDetailsTitle));

    public static string OtherModel => Get(nameof(OtherModel));

    public static string CopyPrompt => Get(nameof(CopyPrompt));

    public static string UsedInSession => Get(nameof(UsedInSession));

    public static string AllModels => Get(nameof(AllModels));

    public static string ModelPickerTitle => Get(nameof(ModelPickerTitle));

    private static string Get(string name) =>
        ResourceManager.GetString(name, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Missing string resource '{name}'.");
}
