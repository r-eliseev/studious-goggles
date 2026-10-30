using ImageGen.Core.Domain.Models;
using ImageGen.Core.Storage;
using Microsoft.Data.Sqlite;

namespace ImageGen.Data.Repositories;

public sealed class ImageModelRepository(SqliteDatabase database) : IImageModelRepository
{
    public async Task ReplaceAllAsync(string providerId, IReadOnlyList<ImageModel> models, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentNullException.ThrowIfNull(models);
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM image_models WHERE provider_id = @provider_id;";
            delete.Parameters.AddWithValue("@provider_id", providerId);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var model in models)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO image_models (provider_id, id, name, supports_seed, supports_image_input, max_images_per_request, fetched_at, supported_parameters_json, prices_json)
                VALUES (@provider_id, @id, @name, @supports_seed, @supports_image_input, @max_images_per_request, @fetched_at, @supported_parameters_json, @prices_json);
                """;
            insert.Parameters.AddWithValue("@provider_id", providerId);
            insert.Parameters.AddWithValue("@id", model.Id);
            insert.Parameters.AddWithValue("@name", model.Name);
            insert.Parameters.AddWithValue("@supports_seed", SqliteValues.FromBool(model.SupportsSeed));
            insert.Parameters.AddWithValue("@supports_image_input", SqliteValues.FromBool(model.SupportsImageInput));
            insert.Parameters.AddWithValue("@max_images_per_request", model.MaxImagesPerRequest);
            insert.Parameters.AddWithValue("@fetched_at", SqliteValues.FromDate(model.FetchedAt));
            insert.Parameters.AddWithValue("@supported_parameters_json", SqliteValues.FromNullableString(ParameterJson.Catalog(model.Parameters)));
            insert.Parameters.AddWithValue("@prices_json", ParameterJson.Prices(model.Prices));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ImageModel>> ListAsync(string providerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT provider_id, id, name, supports_seed, supports_image_input, max_images_per_request, fetched_at, supported_parameters_json, prices_json
            FROM image_models
            WHERE provider_id = @provider_id
            ORDER BY name ASC;
            """;
        command.Parameters.AddWithValue("@provider_id", providerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<ImageModel>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ImageModel
            {
                ProviderId = SqliteValues.GetString(reader, "provider_id"),
                Id = SqliteValues.GetString(reader, "id"),
                Name = SqliteValues.GetString(reader, "name"),
                SupportsSeed = SqliteValues.GetBool(reader, "supports_seed"),
                SupportsImageInput = SqliteValues.GetBool(reader, "supports_image_input"),
                MaxImagesPerRequest = SqliteValues.GetInt32(reader, "max_images_per_request"),
                FetchedAt = SqliteValues.GetDate(reader, "fetched_at"),
                Parameters = ParameterJson.ReadCatalog(SqliteValues.GetNullableString(reader, "supported_parameters_json")),
                Prices = ParameterJson.ReadPrices(SqliteValues.GetNullableString(reader, "prices_json")),
            });
        }

        return result;
    }
}
