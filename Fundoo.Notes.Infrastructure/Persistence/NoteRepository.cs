using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Domain.Entities;
using Npgsql;
using NpgsqlTypes;

namespace Fundoo.Notes.Infrastructure.Persistence;

public class NoteRepository(NpgsqlDataSource dataSource) : INoteRepository
{
    private const string NoteColumns =
        "n.id, n.user_id, n.title, n.content, n.color, n.is_pinned, n.is_archived, n.is_trashed, n.created_at_utc, n.updated_at_utc";

    public async Task<Note?> GetOwnedAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);

        var notes = new List<Note>();
        await using (var cmd = new NpgsqlCommand(
            $"SELECT {NoteColumns} FROM notes n WHERE n.id = @id AND n.user_id = @userId", conn))
        {
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("userId", userId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                notes.Add(ReadNote(reader));
        }

        await LoadLabelsAsync(conn, notes, ct);
        return notes.FirstOrDefault();
    }

    public async Task<List<Note>> ListAsync(Guid userId, NoteView view, CancellationToken ct = default)
    {
        // filter sirf hamare fixed strings hain, user ki koi value SQL text mein nahi judti
        var filter = view switch
        {
            NoteView.Active => "AND n.is_archived = FALSE AND n.is_trashed = FALSE",
            NoteView.Archived => "AND n.is_archived = TRUE AND n.is_trashed = FALSE",
            NoteView.Trashed => "AND n.is_trashed = TRUE",
            _ => ""
        };

        var sql = $"""
            SELECT {NoteColumns}
            FROM notes n
            WHERE n.user_id = @userId {filter}
            ORDER BY n.is_pinned DESC, n.updated_at_utc DESC
            """;

        await using var conn = await dataSource.OpenConnectionAsync(ct);

        var notes = new List<Note>();
        await using (var cmd = new NpgsqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("userId", userId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                notes.Add(ReadNote(reader));
        }

        await LoadLabelsAsync(conn, notes, ct);
        return notes;
    }

    public async Task AddAsync(Note note, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO notes (id, user_id, title, content, color, is_pinned, is_archived, is_trashed, created_at_utc, updated_at_utc)
            VALUES (@id, @userId, @title, @content, @color, @isPinned, @isArchived, @isTrashed, @createdAt, @updatedAt)
            """;

        await using var cmd = dataSource.CreateCommand(sql);
        AddNoteParameters(cmd, note);
        cmd.Parameters.AddWithValue("createdAt", note.CreatedAtUtc);
        cmd.Parameters.AddWithValue("updatedAt", note.UpdatedAtUtc);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateAsync(Note note, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE notes
            SET title = @title, content = @content, color = @color,
                is_pinned = @isPinned, is_archived = @isArchived, is_trashed = @isTrashed,
                updated_at_utc = @updatedAt
            WHERE id = @id AND user_id = @userId
            """;

        await using var cmd = dataSource.CreateCommand(sql);
        AddNoteParameters(cmd, note);
        cmd.Parameters.AddWithValue("updatedAt", note.UpdatedAtUtc);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(Note note, CancellationToken ct = default)
    {
        await using var cmd = dataSource.CreateCommand(
            "DELETE FROM notes WHERE id = @id AND user_id = @userId");
        cmd.Parameters.AddWithValue("id", note.Id);
        cmd.Parameters.AddWithValue("userId", note.UserId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void AddNoteParameters(NpgsqlCommand cmd, Note n)
    {
        cmd.Parameters.AddWithValue("id", n.Id);
        cmd.Parameters.AddWithValue("userId", n.UserId);
        cmd.Parameters.AddWithValue("title", n.Title);
        cmd.Parameters.AddWithValue("content", n.Content);
        cmd.Parameters.Add(new NpgsqlParameter("color", NpgsqlDbType.Varchar)
        {
            Value = (object?)n.Color ?? DBNull.Value
        });
        cmd.Parameters.AddWithValue("isPinned", n.IsPinned);
        cmd.Parameters.AddWithValue("isArchived", n.IsArchived);
        cmd.Parameters.AddWithValue("isTrashed", n.IsTrashed);
    }

    private static Note ReadNote(NpgsqlDataReader r) => new()
    {
        Id = r.GetGuid(0),
        UserId = r.GetGuid(1),
        Title = r.GetString(2),
        Content = r.GetString(3),
        Color = r.IsDBNull(4) ? null : r.GetString(4),
        IsPinned = r.GetBoolean(5),
        IsArchived = r.GetBoolean(6),
        IsTrashed = r.GetBoolean(7),
        CreatedAtUtc = r.GetDateTime(8),
        UpdatedAtUtc = r.GetDateTime(9)
    };

    // Ek hi query mein saare notes ke labels (N+1 problem se bachne ke liye)
    private static async Task LoadLabelsAsync(NpgsqlConnection conn, List<Note> notes, CancellationToken ct)
    {
        if (notes.Count == 0) return;

        var byId = notes.ToDictionary(n => n.Id);

        await using var cmd = new NpgsqlCommand("""
            SELECT nl.note_id, l.id, l.user_id, l.name
            FROM note_labels nl
            JOIN labels l ON l.id = nl.label_id
            WHERE nl.note_id = ANY(@noteIds)
            ORDER BY l.name
            """, conn);
        cmd.Parameters.AddWithValue("noteIds", byId.Keys.ToArray());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var note = byId[reader.GetGuid(0)];
            var label = new Label
            {
                Id = reader.GetGuid(1),
                UserId = reader.GetGuid(2),
                Name = reader.GetString(3)
            };
            note.NoteLabels.Add(new NoteLabel { NoteId = note.Id, Note = note, LabelId = label.Id, Label = label });
        }
    }
}