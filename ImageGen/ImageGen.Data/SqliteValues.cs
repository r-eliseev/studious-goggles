using System.Globalization;
using Microsoft.Data.Sqlite;

namespace ImageGen.Data;

/// <summary>Conversions between .NET values and SQLite column values.</summary>
internal static class SqliteValues
{
    public static string FromGuid(Guid value) => value.ToString("D");

    public static object FromNullableGuid(Guid? value) => value.HasValue ? FromGuid(value.Value) : DBNull.Value;

    public static string FromDate(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);

    public static object FromNullableDate(DateTimeOffset? value) => value.HasValue ? FromDate(value.Value) : DBNull.Value;

    public static object FromNullableDecimal(decimal? value) =>
        value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : DBNull.Value;

    public static string FromDecimal(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    public static decimal GetDecimal(SqliteDataReader reader, string column) =>
        decimal.Parse(reader.GetString(reader.GetOrdinal(column)), CultureInfo.InvariantCulture);

    public static object FromNullableString(string? value) => value is null ? DBNull.Value : value;

    public static long FromBool(bool value) => value ? 1 : 0;

    public static Guid GetGuid(SqliteDataReader reader, string column) =>
        Guid.Parse(reader.GetString(reader.GetOrdinal(column)));

    public static Guid? GetNullableGuid(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Guid.Parse(reader.GetString(ordinal));
    }

    public static DateTimeOffset GetDate(SqliteDataReader reader, string column) =>
        DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal(column)), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public static DateTimeOffset? GetNullableDate(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : DateTimeOffset.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    public static decimal? GetNullableDecimal(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : decimal.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture);
    }

    public static string GetString(SqliteDataReader reader, string column) => reader.GetString(reader.GetOrdinal(column));

    public static string? GetNullableString(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    public static long GetInt64(SqliteDataReader reader, string column) => reader.GetInt64(reader.GetOrdinal(column));

    public static int GetInt32(SqliteDataReader reader, string column) => reader.GetInt32(reader.GetOrdinal(column));

    public static int? GetNullableInt32(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    public static bool GetBool(SqliteDataReader reader, string column) => reader.GetInt64(reader.GetOrdinal(column)) != 0;
}
