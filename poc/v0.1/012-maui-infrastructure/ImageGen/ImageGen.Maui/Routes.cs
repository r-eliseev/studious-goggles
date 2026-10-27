namespace ImageGen.Maui;

public static class Routes
{
    public const string ApiKey = "apikey";
    public const string Chat = "chat";
    public const string SessionModels = "models";
    public const string SessionIdParameter = "sessionId";

    public static string ChatFor(Guid sessionId) => $"{Chat}?{SessionIdParameter}={sessionId:D}";

    public static string SessionModelsFor(Guid sessionId) => $"{SessionModels}?{SessionIdParameter}={sessionId:D}";
}
