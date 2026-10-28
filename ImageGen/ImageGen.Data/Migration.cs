namespace ImageGen.Data;

/// <summary>One schema migration: its number (the schema version it moves the database to), file name and SQL text.</summary>
internal sealed record Migration(int Number, string Name, string Script);
