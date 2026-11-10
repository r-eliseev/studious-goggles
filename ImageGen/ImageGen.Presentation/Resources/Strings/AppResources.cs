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

    public static string SessionLongPressHint => Get(nameof(SessionLongPressHint));

    public static string RenameSessionTitle => Get(nameof(RenameSessionTitle));

    public static string Rename => Get(nameof(Rename));

    public static string RenameEmpty => Get(nameof(RenameEmpty));

    public static string RunsCountOne => Get(nameof(RunsCountOne));

    public static string SearchSessions => Get(nameof(SearchSessions));

    public static string ClearSearch => Get(nameof(ClearSearch));

    public static string NothingFound => Get(nameof(NothingFound));

    public static string SearchInTitle => Get(nameof(SearchInTitle));

    public static string SearchInPrompt => Get(nameof(SearchInPrompt));

    public static string SearchInDescription => Get(nameof(SearchInDescription));

    public static string SemanticSearchTitle => Get(nameof(SemanticSearchTitle));

    public static string SemanticSearchOff => Get(nameof(SemanticSearchOff));

    public static string SemanticSearchProgressFormat => Get(nameof(SemanticSearchProgressFormat));

    public static string SemanticSearchReadyFormat => Get(nameof(SemanticSearchReadyFormat));

    public static string SemanticSearchUnavailable => Get(nameof(SemanticSearchUnavailable));

    public static string SemanticSearchFailed => Get(nameof(SemanticSearchFailed));

    public static string SemanticSearchStopped => Get(nameof(SemanticSearchStopped));

    public static string SemanticSearchEnablePromptFormat => Get(nameof(SemanticSearchEnablePromptFormat));

    public static string SemanticSearchDisablePrompt => Get(nameof(SemanticSearchDisablePrompt));

    public static string SemanticSearchEnable => Get(nameof(SemanticSearchEnable));

    public static string SemanticSearchDisable => Get(nameof(SemanticSearchDisable));

    public static string SemanticSearchRetry => Get(nameof(SemanticSearchRetry));

    public static string SemanticSearchLimitWarning => Get(nameof(SemanticSearchLimitWarning));

    public static string SemanticSearchWords => Get(nameof(SemanticSearchWords));

    public static string SemanticSearchMeaning => Get(nameof(SemanticSearchMeaning));

    public static string SemanticSearchMeaningUnavailable => Get(nameof(SemanticSearchMeaningUnavailable));

    public static string SemanticSearchModelSearch => Get(nameof(SemanticSearchModelSearch));

    public static string SemanticSearchChangeModel => Get(nameof(SemanticSearchChangeModel));

    public static string SemanticSearchChangeModelPromptFormat => Get(nameof(SemanticSearchChangeModelPromptFormat));

    public static string SemanticSearchModelUnavailableFormat => Get(nameof(SemanticSearchModelUnavailableFormat));

    public static string SemanticSearchUnavailableOptionFormat => Get(nameof(SemanticSearchUnavailableOptionFormat));

    public static string Stop => Get(nameof(Stop));

    public static string Saved => Get(nameof(Saved));

    public static string SavePermissionDenied => Get(nameof(SavePermissionDenied));

    public static string SaveFailed => Get(nameof(SaveFailed));

    public static string TryAgain => Get(nameof(TryAgain));

    public static string EmptyChatExample1 => Get(nameof(EmptyChatExample1));

    public static string EmptyChatExample2 => Get(nameof(EmptyChatExample2));

    public static string EmptyChatExample3 => Get(nameof(EmptyChatExample3));

    public static string SummaryMarker => Get(nameof(SummaryMarker));

    public static string SummaryTitle => Get(nameof(SummaryTitle));

    public static string ShowSummary => Get(nameof(ShowSummary));

    public static string MenuFile => Get(nameof(MenuFile));

    public static string MenuFind => Get(nameof(MenuFind));

    public static string MenuSettings => Get(nameof(MenuSettings));

    public static string MenuCard => Get(nameof(MenuCard));

    public static string SaveImageAs => Get(nameof(SaveImageAs));

    public static string SaveToPhotos => Get(nameof(SaveToPhotos));

    public static string SaveToGallery => Get(nameof(SaveToGallery));

    /// <summary>Format string: the number of runs goes in <c>{0}</c>.</summary>
    public static string RunsCountFormat => Get(nameof(RunsCountFormat));

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

    public static string VideoModelsTitle => Get(nameof(VideoModelsTitle));

    public static string VideoModelsSelectedFormat => Get(nameof(VideoModelsSelectedFormat));

    public static string AudioModelsTitle => Get(nameof(AudioModelsTitle));

    public static string AudioModelsSelectedFormat => Get(nameof(AudioModelsSelectedFormat));

    public static string AudioMusicSoundGroup => Get(nameof(AudioMusicSoundGroup));

    public static string AudioSpeechGroup => Get(nameof(AudioSpeechGroup));

    public static string AudioPriceUnknown => Get(nameof(AudioPriceUnknown));

    public static string AudioPricePerThousandFormat => Get(nameof(AudioPricePerThousandFormat));

    public static string SessionAudioVoice => Get(nameof(SessionAudioVoice));

    public static string SessionAudioVoiceHint => Get(nameof(SessionAudioVoiceHint));

    public static string StoppedWaitingMedia => Get(nameof(StoppedWaitingMedia));

    public static string PlayAudio => Get(nameof(PlayAudio));

    public static string VideoPriceUnknown => Get(nameof(VideoPriceUnknown));

    public static string VideoPriceForDurationFormat => Get(nameof(VideoPriceForDurationFormat));

    public static string VideoAudioBadge => Get(nameof(VideoAudioBadge));

    public static string VideoDuration => Get(nameof(VideoDuration));

    public static string VideoResolution => Get(nameof(VideoResolution));

    public static string VideoAspectRatio => Get(nameof(VideoAspectRatio));

    public static string VideoGenerateAudio => Get(nameof(VideoGenerateAudio));

    public static string VideoDurationFormat => Get(nameof(VideoDurationFormat));

    public static string StoppedWaitingVideo => Get(nameof(StoppedWaitingVideo));

    public static string SaveFile => Get(nameof(SaveFile));

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

    public static string SettingsShareLogs => Get(nameof(SettingsShareLogs));

    public static string SettingsShareLogsTitle => Get(nameof(SettingsShareLogsTitle));

    public static string SettingsClearLogs => Get(nameof(SettingsClearLogs));

    public static string SettingsClearLogsConfirm => Get(nameof(SettingsClearLogsConfirm));

    public static string SettingsClearHistory => Get(nameof(SettingsClearHistory));

    public static string SettingsClearHistoryConfirm => Get(nameof(SettingsClearHistoryConfirm));

    public static string SettingsHistoryDeletedFormat => Get(nameof(SettingsHistoryDeletedFormat));

    public static string SettingsLogsCleared => Get(nameof(SettingsLogsCleared));

    public static string SettingsLogsClearedWithFailures => Get(nameof(SettingsLogsClearedWithFailures));

    public static string SettingsNoLogs => Get(nameof(SettingsNoLogs));

    public static string SessionsToday => Get(nameof(SessionsToday));

    public static string SessionsEarlier => Get(nameof(SessionsEarlier));

    public static string EmptyChatTitle => Get(nameof(EmptyChatTitle));

    public static string EmptyChatHint => Get(nameof(EmptyChatHint));

    public static string ApiKeyHeading => Get(nameof(ApiKeyHeading));

    public static string ProviderKeyTitleFormat => Get(nameof(ProviderKeyTitleFormat));

    public static string ProviderKeyDescriptionFormat => Get(nameof(ProviderKeyDescriptionFormat));

    public static string ProviderKeyPlaceholder => Get(nameof(ProviderKeyPlaceholder));

    public static string ProviderKeyInvalidFormat => Get(nameof(ProviderKeyInvalidFormat));

    public static string ProviderKeyNetworkErrorFormat => Get(nameof(ProviderKeyNetworkErrorFormat));

    public static string ProviderKeyRemoveTitle => Get(nameof(ProviderKeyRemoveTitle));

    public static string ProviderKeyRemoveConfirmFormat => Get(nameof(ProviderKeyRemoveConfirmFormat));

    public static string ProviderKeyRemoveOpenRouterWarning => Get(nameof(ProviderKeyRemoveOpenRouterWarning));

    public static string ProviderKeyRemove => Get(nameof(ProviderKeyRemove));

    public static string ProviderKeyUnknown => Get(nameof(ProviderKeyUnknown));

    public static string SettingsProviders => Get(nameof(SettingsProviders));

    public static string NewSeed => Get(nameof(NewSeed));

    public static string Edit => Get(nameof(Edit));

    public static string EditPromptTitle => Get(nameof(EditPromptTitle));

    public static string EditPromptMessage => Get(nameof(EditPromptMessage));

    public static string Generate => Get(nameof(Generate));

    public static string ModelsTitle => Get(nameof(ModelsTitle));

    public static string ImageModelsTab => Get(nameof(ImageModelsTab));

    public static string ModelsSearchPlaceholder => Get(nameof(ModelsSearchPlaceholder));

    public static string RunsPerModelFormat => Get(nameof(RunsPerModelFormat));

    public static string ModelSupportsSeed => Get(nameof(ModelSupportsSeed));

    public static string ModelNoSeed => Get(nameof(ModelNoSeed));

    public static string ModelGroupOther => Get(nameof(ModelGroupOther));

    public static string ModelGroupImageHeaderFormat => Get(nameof(ModelGroupImageHeaderFormat));

    public static string ModelGroupAssistantHeaderFormat => Get(nameof(ModelGroupAssistantHeaderFormat));

    public static string ModelGroupExpanded => Get(nameof(ModelGroupExpanded));

    public static string ModelGroupCollapsed => Get(nameof(ModelGroupCollapsed));

    public static string Save => Get(nameof(Save));

    public static string Refresh => Get(nameof(Refresh));

    public static string AssistantModelTitle => Get(nameof(AssistantModelTitle));

    public static string AssistantLabelFormat => Get(nameof(AssistantLabelFormat));

    public static string AssistantPriceFormat => Get(nameof(AssistantPriceFormat));

    public static string AssistantPriceUnknown => Get(nameof(AssistantPriceUnknown));

    public static string NextRunEstimateFormat => Get(nameof(NextRunEstimateFormat));

    public static string CostEstimateWithUnknownFormat => Get(nameof(CostEstimateWithUnknownFormat));

    public static string SpendingWarningTitle => Get(nameof(SpendingWarningTitle));

    public static string SpendingMonthlyLimitMessage => Get(nameof(SpendingMonthlyLimitMessage));

    public static string SpendingConfirmationFormat => Get(nameof(SpendingConfirmationFormat));

    public static string SpendingTitle => Get(nameof(SpendingTitle));
    public static string SpendingPeriod => Get(nameof(SpendingPeriod));
    public static string SpendingDay => Get(nameof(SpendingDay));
    public static string SpendingWeek => Get(nameof(SpendingWeek));
    public static string SpendingMonth => Get(nameof(SpendingMonth));
    public static string SpendingTotal => Get(nameof(SpendingTotal));
    public static string SpendingUnknownOperationsFormat => Get(nameof(SpendingUnknownOperationsFormat));
    public static string SpendingMonthlyLimit => Get(nameof(SpendingMonthlyLimit));
    public static string SpendingExpensiveThreshold => Get(nameof(SpendingExpensiveThreshold));
    public static string SpendingSave => Get(nameof(SpendingSave));
    public static string SpendingBack => Get(nameof(SpendingBack));
    public static string SpendingInvalidAmount => Get(nameof(SpendingInvalidAmount));
    public static string SettingsSpending => Get(nameof(SettingsSpending));

    public static string GalleryTitle => Get(nameof(GalleryTitle));
    public static string GalleryAllModels => Get(nameof(GalleryAllModels));
    public static string GalleryAllTime => Get(nameof(GalleryAllTime));
    public static string GalleryLast7Days => Get(nameof(GalleryLast7Days));
    public static string GalleryLast30Days => Get(nameof(GalleryLast30Days));
    public static string GalleryUnavailableMessage => Get(nameof(GalleryUnavailableMessage));
    public static string GalleryLoadMore => Get(nameof(GalleryLoadMore));
    public static string GalleryOpen => Get(nameof(GalleryOpen));
    public static string SessionsGallery => Get(nameof(SessionsGallery));
    public static string GalleryPeriod => Get(nameof(GalleryPeriod));
    public static string GalleryModel => Get(nameof(GalleryModel));
    public static string GalleryLoadFailed => Get(nameof(GalleryLoadFailed));

    public static string ImagePricePerImageFormat => Get(nameof(ImagePricePerImageFormat));

    public static string ImagePricePerMegapixelFormat => Get(nameof(ImagePricePerMegapixelFormat));

    public static string ImagePricePerMillionTokensFormat => Get(nameof(ImagePricePerMillionTokensFormat));

    public static string ImagePricePerRequestFormat => Get(nameof(ImagePricePerRequestFormat));

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

    public static string ImageDimensionsFormat => Get(nameof(ImageDimensionsFormat));

    public static string ImageViewerOpen => Get(nameof(ImageViewerOpen));

    public static string ImageViewerClose => Get(nameof(ImageViewerClose));

    public static string PlayVideo => Get(nameof(PlayVideo));

    public static string ImageViewerPrevious => Get(nameof(ImageViewerPrevious));

    public static string ImageViewerNext => Get(nameof(ImageViewerNext));

    public static string ImageViewerPositionFormat => Get(nameof(ImageViewerPositionFormat));

    public static string ImageViewerUnreadable => Get(nameof(ImageViewerUnreadable));

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

    public static string ErrorKindUnknown => Get(nameof(ErrorKindUnknown));

    public static string ErrorKindInvalidKey => Get(nameof(ErrorKindInvalidKey));

    public static string ErrorKindInsufficientFunds => Get(nameof(ErrorKindInsufficientFunds));

    public static string ErrorKindModerated => Get(nameof(ErrorKindModerated));

    public static string ErrorKindForbidden => Get(nameof(ErrorKindForbidden));

    public static string ErrorKindBadRequest => Get(nameof(ErrorKindBadRequest));

    public static string ErrorKindTimeout => Get(nameof(ErrorKindTimeout));

    public static string ErrorKindRateLimited => Get(nameof(ErrorKindRateLimited));

    public static string ErrorKindModelUnavailable => Get(nameof(ErrorKindModelUnavailable));

    public static string ErrorKindNetwork => Get(nameof(ErrorKindNetwork));

    public static string ErrorKindInterrupted => Get(nameof(ErrorKindInterrupted));

    public static string ErrorKindEmptyResponse => Get(nameof(ErrorKindEmptyResponse));

    public static string ErrorKindInvalidToolCall => Get(nameof(ErrorKindInvalidToolCall));

    public static string ErrorKindAttachmentUnreadable => Get(nameof(ErrorKindAttachmentUnreadable));

    public static string ErrorKindReferenceUnreadable => Get(nameof(ErrorKindReferenceUnreadable));

    public static string ErrorKindModelWithoutReferences => Get(nameof(ErrorKindModelWithoutReferences));

    public static string ErrorKindImageSaveFailed => Get(nameof(ErrorKindImageSaveFailed));

    public static string DescriptionModelDoesNotAcceptImages => Get(nameof(DescriptionModelDoesNotAcceptImages));

    public static string DescriptionSourceMissing => Get(nameof(DescriptionSourceMissing));

    public static string DescriptionEmptyResponse => Get(nameof(DescriptionEmptyResponse));

    public static string OtherModel => Get(nameof(OtherModel));

    public static string CopyPrompt => Get(nameof(CopyPrompt));

    public static string Compare => Get(nameof(Compare));

    public static string ComparisonTitle => Get(nameof(ComparisonTitle));

    public static string ComparisonClose => Get(nameof(ComparisonClose));

    public static string ComparisonFirstImage => Get(nameof(ComparisonFirstImage));

    public static string ComparisonSecondImage => Get(nameof(ComparisonSecondImage));

    public static string ComparisonCandidateFormat => Get(nameof(ComparisonCandidateFormat));

    public static string ComparisonNoCandidates => Get(nameof(ComparisonNoCandidates));

    public static string ComparisonUnavailable => Get(nameof(ComparisonUnavailable));

    public static string ComparisonSideBySide => Get(nameof(ComparisonSideBySide));

    public static string ComparisonOverlay => Get(nameof(ComparisonOverlay));

    public static string ComparisonZoom => Get(nameof(ComparisonZoom));

    public static string CancelRun => Get(nameof(CancelRun));

    public static string CancelRequested => Get(nameof(CancelRequested));

    public static string CancelFailed => Get(nameof(CancelFailed));

    public static string UsedInSession => Get(nameof(UsedInSession));

    public static string AllModels => Get(nameof(AllModels));

    public static string ModelPickerTitle => Get(nameof(ModelPickerTitle));

    public static string FirstRunProviderPrompt => Get(nameof(FirstRunProviderPrompt));

    public static string FirstRunAssistantModelPrompt => Get(nameof(FirstRunAssistantModelPrompt));

    public static string NoAssistantModelsConfigured => Get(nameof(NoAssistantModelsConfigured));

    public static string UseAssistantModel => Get(nameof(UseAssistantModel));

    public static string SettingsDefaultAssistantModel => Get(nameof(SettingsDefaultAssistantModel));

    public static string SettingsSaveAssistantModel => Get(nameof(SettingsSaveAssistantModel));

    private static string Get(string name) =>
        ResourceManager.GetString(name, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Missing string resource '{name}'.");
}
