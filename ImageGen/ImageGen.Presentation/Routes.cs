namespace ImageGen.Presentation;

/// <summary>Shell route names and the query parameter that carries the session id.</summary>
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

    public static string ChatFor(Guid sessionId) => $"{Chat}?{SessionIdParameter}={sessionId:D}";

    public static string SessionModelsFor(Guid sessionId) => $"{SessionModels}?{SessionIdParameter}={sessionId:D}";

    public static string SessionParametersFor(Guid sessionId) => $"{SessionParameters}?{SessionIdParameter}={sessionId:D}";

    public static string ModelPickerFor(Guid sessionId, Guid generationId) =>
        $"{ModelPicker}?{SessionIdParameter}={sessionId:D}&{GenerationIdParameter}={generationId:D}";
}
