namespace ImageGen.Presentation;

/// <summary>Shell route names and their query parameters.</summary>
public static class Routes
{
    public const string Sessions = "sessions";
    public const string ApiKey = "apikey";
    public const string Chat = "chat";
    public const string SessionModels = "models";
    public const string SessionParameters = "parameters";
    public const string Settings = "settings";
    public const string ModelPicker = "modelpicker";
    public const string SessionIdParameter = "sessionId";
    public const string GenerationIdParameter = "generationId";
    public const string MessageParameter = "message";
    public const string ProviderIdParameter = "providerId";

    public static string ApiKeyFor(string providerId) =>
        $"{ApiKey}?{ProviderIdParameter}={Uri.EscapeDataString(providerId)}";

    public static string ChatFor(Guid sessionId) => $"{Chat}?{SessionIdParameter}={sessionId:D}";

    /// <summary>The chat of a session that sends <paramref name="message"/> to the assistant once it has loaded (S1.5 examples).</summary>
    public static string ChatWithMessage(Guid sessionId, string message) =>
        $"{Chat}?{SessionIdParameter}={sessionId:D}&{MessageParameter}={Uri.EscapeDataString(message)}";

    public static string SessionModelsFor(Guid sessionId) => $"{SessionModels}?{SessionIdParameter}={sessionId:D}";

    public static string SessionParametersFor(Guid sessionId) => $"{SessionParameters}?{SessionIdParameter}={sessionId:D}";

    public static string ModelPickerFor(Guid sessionId, Guid generationId) =>
        $"{ModelPicker}?{SessionIdParameter}={sessionId:D}&{GenerationIdParameter}={generationId:D}";
}
