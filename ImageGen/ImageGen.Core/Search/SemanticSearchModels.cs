namespace ImageGen.Core.Search;

public static class SemanticSearchModels
{
    public const string Default = "baai/bge-m3";

    public static bool IsDefault(string model) =>
        string.Equals(model, Default, StringComparison.OrdinalIgnoreCase);
}
