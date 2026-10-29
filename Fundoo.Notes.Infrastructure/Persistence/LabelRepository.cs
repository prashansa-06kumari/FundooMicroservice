using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Domain.Entities;
using Npgsql;

namespace Fundoo.Notes.Infrastructure.Persistence;

public class LabelRepository(NpgsqlDataSource dataSource) : ILabelRepository
{
    public async Task<List<Label>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        await using var cmd = dataSource.CreateCommand(
            "SELECT id, user_id, name FROM labels WHERE user_id = @userId ORDER BY name");
        cmd.Parameters.AddWithValue("userId", userId);

        var result = new List<Label>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(ReadLabel(reader));
        return result;
    }

    public async Task<Label?> GetOwnedAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        await using var cmd = dataSource.CreateCommand(
            "SELECT id, user_id, name FROM labels WHERE id = @id AND user_id = @userId");
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("userId", userId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadLabel(reader) : null;
    }

    public async Task<bool> NameExistsAsync(Guid userId, string name, Guid? excludeId, CancellationToken ct = default)
    {
        await using var cmd = dataSource.CreateCommand("""
            SELECT EXISTS (
                SELECT 1 FROM labels
                WHERE user_id = @userId
                  AND lower(name) = lower(@name)
                  AND (@excludeId::uuid IS NULL OR id <> @excludeId::uuid))
            """);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.Add(new NpgsqlParameter("excludeId", NpgsqlTypes.NpgsqlDbType.Uuid)
        {
            Value = excludeId.HasValue ? excludeId.Value : DBNull.Value
        });
        return (bool)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task AddAsync(Label label, CancellationToken ct = default)
    {
        await using var cmd = dataSource.CreateCommand(
            "INSERT INTO labels (id, user_id, name) VALUES (@id, @userId, @name)");
        cmd.Parameters.AddWithValue("id", label.Id);
        cmd.Parameters.AddWithValue("userId", label.UserId);
        cmd.Parameters.AddWithValue("name", label.Name);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateAsync(Label label, CancellationToken ct = default)
    {
        await using var cmd = dataSource.CreateCommand(
            "UPDATE labels SET name = @name WHERE id = @id AND user_id = @userId");
        cmd.Parameters.AddWithValue("id", label.Id);
        cmd.Parameters.AddWithValue("userId", label.UserId);
        cmd.Parameters.AddWithValue("name", label.Name);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(Label label, CancellationToken ct = default)
    {
        await using var cmd = dataSource.CreateCommand(
            "DELETE FROM labels WHERE id = @id AND user_id = @userId");
        cmd.Parameters.AddWithValue("id", label.Id);
        cmd.Parameters.AddWithValue("userId", label.UserId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<NoteLabel?> FindLinkAsync(Guid noteId, Guid labelId, CancellationToken ct = default)
    {
        await using var cmd = dataSource.CreateCommand(
            "SELECT note_id, label_id FROM note_labels WHERE note_id = @noteId AND label_id = @labelId");
        cmd.Parameters.AddWithValue("noteId", noteId);
        cmd.Parameters.AddWithValue("labelId", labelId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new NoteLabel { NoteId = reader.GetGuid(0), LabelId = reader.GetGuid(1) };
    }

    public async Task AddLinkAsync(NoteLabel link, CancellationToken ct = default)
    {
        await using var cmd = dataSource.CreateCommand("""
            INSERT INTO note_labels (note_id, label_id) VALUES (@noteId, @labelId)
            ON CONFLICT (note_id, label_id) DO NOTHING
            """);
        cmd.Parameters.AddWithValue("noteId", link.NoteId);
        cmd.Parameters.AddWithValue("labelId", link.LabelId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task RemoveLinkAsync(NoteLabel link, CancellationToken ct = default)
    {
        await using var cmd = dataSource.CreateCommand(
            "DELETE FROM note_labels WHERE note_id = @noteId AND label_id = @labelId");
        cmd.Parameters.AddWithValue("noteId", link.NoteId);
        cmd.Parameters.AddWithValue("labelId", link.LabelId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static Label ReadLabel(NpgsqlDataReader r) => new()
    {
        Id = r.GetGuid(0),
        UserId = r.GetGuid(1),
        Name = r.GetString(2)
    };
}