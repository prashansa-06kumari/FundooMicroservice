using Fundoo.Notes.Application.Interfaces;
using Fundoo.Notes.Domain.Entities;
using Fundoo.Notes.Infrastructure.Data;
using Npgsql;

namespace Fundoo.Notes.Infrastructure.Repositories;

public class LabelRepository : ILabelRepository
{
    private readonly NotesConnectionFactory _connectionFactory;

    public LabelRepository(NotesConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Label?> CreateAsync(
        Label label,
        CancellationToken ct)
    {
        label.Name = label.Name.Trim();

        if (string.IsNullOrWhiteSpace(label.Name))
        {
            throw new ArgumentException("Label name is required.");
        }

        const string sql = """
            INSERT INTO "Labels" ("Id", "UserId", "Name")
            VALUES (@Id, @UserId, @Name)
            ON CONFLICT ("UserId", "Name") DO NOTHING
            RETURNING "Id", "UserId", "Name";
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("Id", label.Id);
        command.Parameters.AddWithValue("UserId", label.UserId);
        command.Parameters.AddWithValue("Name", label.Name);

        await using var reader = await command.ExecuteReaderAsync(ct);

        return await reader.ReadAsync(ct) ? ReadLabel(reader) : null;
    }

    public async Task<List<Label>> GetAllAsync(
        Guid userId,
        CancellationToken ct)
    {
        const string sql = """
            SELECT "Id", "UserId", "Name"
            FROM "Labels"
            WHERE "UserId" = @UserId
            ORDER BY "Name";
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("UserId", userId);

        var labels = new List<Label>();

        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            labels.Add(ReadLabel(reader));
        }

        return labels;
    }

    public async Task<Label?> GetByIdAsync(
        Guid id,
        Guid userId,
        CancellationToken ct)
    {
        const string sql = """
            SELECT "Id", "UserId", "Name"
            FROM "Labels"
            WHERE "Id" = @Id
              AND "UserId" = @UserId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("Id", id);
        command.Parameters.AddWithValue("UserId", userId);

        await using var reader = await command.ExecuteReaderAsync(ct);

        return await reader.ReadAsync(ct) ? ReadLabel(reader) : null;
    }

    public async Task<bool> UpdateAsync(
        Label label,
        CancellationToken ct)
    {
        var name = label.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        const string sql = """
            UPDATE "Labels"
            SET "Name" = @Name
            WHERE "Id" = @Id
              AND "UserId" = @UserId
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM "Labels" other
                  WHERE other."UserId" = @UserId
                    AND other."Name" = @Name
                    AND other."Id" <> @Id
              );
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("Id", label.Id);
        command.Parameters.AddWithValue("UserId", label.UserId);
        command.Parameters.AddWithValue("Name", name);

        try
        {
            return await command.ExecuteNonQueryAsync(ct) > 0;
        }
        catch (PostgresException ex)
            when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return false;
        }
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        Guid userId,
        CancellationToken ct)
    {
        const string sql = """
            DELETE FROM "Labels"
            WHERE "Id" = @Id
              AND "UserId" = @UserId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("Id", id);
        command.Parameters.AddWithValue("UserId", userId);

        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    private static Label ReadLabel(NpgsqlDataReader reader)
    {
        return new Label
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
            Name = reader.GetString(reader.GetOrdinal("Name"))
        };
    }
}