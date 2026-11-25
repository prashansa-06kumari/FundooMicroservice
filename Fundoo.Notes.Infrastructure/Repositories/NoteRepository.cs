using Fundoo.Notes.Application.Interfaces;
using Fundoo.Notes.Domain.Entities;
using Fundoo.Notes.Infrastructure.Data;
using Npgsql;
using NpgsqlTypes;

namespace Fundoo.Notes.Infrastructure.Repositories;

public class NoteRepository : INoteRepository
{
    private readonly NotesConnectionFactory _connectionFactory;

    public NoteRepository(NotesConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Note> CreateAsync(
        Note note,
        CancellationToken ct)
    {
        const string sql = """
            INSERT INTO "Notes"
            (
                "Id", "UserId", "Title", "Content", "Color",
                "IsPinned", "IsArchived", "IsTrashed",
                "CreatedAtUtc", "UpdatedAtUtc"
            )
            VALUES
            (
                @Id, @UserId, @Title, @Content, @Color,
                @IsPinned, @IsArchived, @IsTrashed,
                @CreatedAtUtc, @UpdatedAtUtc
            );
            """;

        await using var connection =
            _connectionFactory.CreateConnection();

        await connection.OpenAsync(ct);

        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("Id", note.Id);
        command.Parameters.AddWithValue("UserId", note.UserId);
        command.Parameters.AddWithValue("Title", note.Title);
        command.Parameters.AddWithValue("Content", note.Content);

        command.Parameters.AddWithValue(
            "Color",
            (object?)note.Color ?? DBNull.Value);

        command.Parameters.AddWithValue("IsPinned", note.IsPinned);
        command.Parameters.AddWithValue("IsArchived", note.IsArchived);
        command.Parameters.AddWithValue("IsTrashed", note.IsTrashed);
        command.Parameters.AddWithValue("CreatedAtUtc", note.CreatedAtUtc);
        command.Parameters.AddWithValue("UpdatedAtUtc", note.UpdatedAtUtc);

        await command.ExecuteNonQueryAsync(ct);

        return note;
    }

    public async Task<Note?> GetByIdAsync(
        Guid id,
        Guid userId,
        string? userEmail = null,
        CancellationToken ct = default)
    {
        userEmail = userEmail?.Trim().ToLowerInvariant();

        const string sql = """
            SELECT n.*
            FROM "Notes" n
            WHERE n."Id" = @Id
              AND (
                  n."UserId" = @UserId
                  OR (@UserEmail IS NOT NULL AND EXISTS (
                      SELECT 1 FROM "Collaborators" c
                      WHERE c."NoteId" = n."Id" AND LOWER(c."CollaboratorEmail") = @UserEmail
                  ))
              );
            """;

        await using var connection =
            _connectionFactory.CreateConnection();

        await connection.OpenAsync(ct);

        Note? note;

        await using (var command = new NpgsqlCommand(sql, connection))
        {
            command.Parameters.AddWithValue("Id", id);
            command.Parameters.AddWithValue("UserId", userId);
            command.Parameters.AddWithValue("UserEmail", (object?)userEmail ?? DBNull.Value);

            await using var reader = await command.ExecuteReaderAsync(ct);

            note = await reader.ReadAsync(ct)
                ? ReadNote(reader)
                : null;
        }

        if (note != null)
        {
            await LoadLabelsAsync(connection, new[] { note }, ct);
            await LoadCollaboratorsAsync(connection, new[] { note }, ct);
        }

        return note;
    }

    public async Task<List<Note>> GetAllAsync(
        Guid userId,
        string? userEmail = null,
        CancellationToken ct = default)
    {
        userEmail = userEmail?.Trim().ToLowerInvariant();

        const string sql = """
            SELECT DISTINCT n.*
            FROM "Notes" n
            LEFT JOIN "Collaborators" c ON c."NoteId" = n."Id"
            WHERE (
                n."UserId" = @UserId
                OR (@UserEmail IS NOT NULL AND LOWER(c."CollaboratorEmail") = @UserEmail)
            )
              AND n."IsArchived" = FALSE
              AND n."IsTrashed" = FALSE
            ORDER BY n."IsPinned" DESC, n."UpdatedAtUtc" DESC;
            """;

        return await GetNotesAsync(sql, userId, userEmail, ct);
    }

    public async Task<List<Note>> GetArchivedAsync(
        Guid userId,
        CancellationToken ct)
    {
        const string sql = """
            SELECT *
            FROM "Notes"
            WHERE "UserId" = @UserId
              AND "IsArchived" = TRUE
              AND "IsTrashed" = FALSE
            ORDER BY "UpdatedAtUtc" DESC;
            """;

        return await GetNotesAsync(sql, userId, null, ct);
    }

    public async Task<List<Note>> GetTrashAsync(
        Guid userId,
        CancellationToken ct)
    {
        const string sql = """
            SELECT *
            FROM "Notes"
            WHERE "UserId" = @UserId
              AND "IsTrashed" = TRUE
            ORDER BY "UpdatedAtUtc" DESC;
            """;

        return await GetNotesAsync(sql, userId, null, ct);
    }

    private async Task<List<Note>> GetNotesAsync(
        string sql,
        Guid userId,
        string? userEmail,
        CancellationToken ct)
    {
        await using var connection =
            _connectionFactory.CreateConnection();

        await connection.OpenAsync(ct);

        var notes = new List<Note>();

        await using (var command = new NpgsqlCommand(sql, connection))
        {
            command.Parameters.AddWithValue("UserId", userId);
            command.Parameters.AddWithValue("UserEmail", (object?)userEmail ?? DBNull.Value);

            await using var reader = await command.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                notes.Add(ReadNote(reader));
            }
        }

        if (notes.Count > 0)
        {
            await LoadLabelsAsync(connection, notes, ct);
            await LoadCollaboratorsAsync(connection, notes, ct);
        }

        return notes;
    }

    public async Task<bool> UpdateAsync(
        Note note,
        string? userEmail = null,
        CancellationToken ct = default)
    {
        userEmail = userEmail?.Trim().ToLowerInvariant();

        const string sql = """
            UPDATE "Notes" n
            SET "Title" = @Title,
                "Content" = @Content,
                "Color" = @Color,
                "IsPinned" = @IsPinned,
                "UpdatedAtUtc" = @UpdatedAtUtc
            WHERE n."Id" = @Id
              AND (
                  n."UserId" = @UserId
                  OR (@UserEmail IS NOT NULL AND EXISTS (
                      SELECT 1 FROM "Collaborators" c
                      WHERE c."NoteId" = n."Id" AND LOWER(c."CollaboratorEmail") = @UserEmail
                  ))
              );
            """;

        await using var connection =
            _connectionFactory.CreateConnection();

        await connection.OpenAsync(ct);

        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("Id", note.Id);
        command.Parameters.AddWithValue("UserId", note.UserId);
        command.Parameters.AddWithValue("UserEmail", (object?)userEmail ?? DBNull.Value);
        command.Parameters.AddWithValue("Title", note.Title);
        command.Parameters.AddWithValue("Content", note.Content);

        command.Parameters.AddWithValue(
            "Color",
            (object?)note.Color ?? DBNull.Value);

        command.Parameters.AddWithValue("IsPinned", note.IsPinned);
        command.Parameters.AddWithValue(
            "UpdatedAtUtc", DateTime.UtcNow);

        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        Guid userId,
        CancellationToken ct)
    {
        // Permanent deletion is allowed only after the note is trashed.
        const string sql = """
            DELETE FROM "Notes"
            WHERE "Id" = @Id
              AND "UserId" = @UserId
              AND "IsTrashed" = TRUE;
            """;

        await using var connection =
            _connectionFactory.CreateConnection();

        await connection.OpenAsync(ct);

        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("Id", id);
        command.Parameters.AddWithValue("UserId", userId);

        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> SetArchivedAsync(
        Guid id,
        Guid userId,
        bool archived,
        CancellationToken ct)
    {
        const string sql = """
            UPDATE "Notes"
            SET "IsArchived" = @Archived,
                "IsPinned" = CASE
                    WHEN @Archived THEN FALSE
                    ELSE "IsPinned"
                END,
                "UpdatedAtUtc" = @UpdatedAtUtc
            WHERE "Id" = @Id
              AND "UserId" = @UserId
              AND "IsTrashed" = FALSE;
            """;

        await using var connection =
            _connectionFactory.CreateConnection();

        await connection.OpenAsync(ct);

        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("Id", id);
        command.Parameters.AddWithValue("UserId", userId);
        command.Parameters.AddWithValue("Archived", archived);
        command.Parameters.AddWithValue(
            "UpdatedAtUtc", DateTime.UtcNow);

        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> SetTrashedAsync(
        Guid id,
        Guid userId,
        bool trashed,
        CancellationToken ct)
    {
        const string sql = """
            UPDATE "Notes"
            SET "IsTrashed" = @Trashed,
                "IsArchived" = CASE
                    WHEN @Trashed THEN FALSE
                    ELSE "IsArchived"
                END,
                "IsPinned" = CASE
                    WHEN @Trashed THEN FALSE
                    ELSE "IsPinned"
                END,
                "UpdatedAtUtc" = @UpdatedAtUtc
            WHERE "Id" = @Id
              AND "UserId" = @UserId;
            """;

        await using var connection =
            _connectionFactory.CreateConnection();

        await connection.OpenAsync(ct);

        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("Id", id);
        command.Parameters.AddWithValue("UserId", userId);
        command.Parameters.AddWithValue("Trashed", trashed);
        command.Parameters.AddWithValue(
            "UpdatedAtUtc", DateTime.UtcNow);

        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> AddLabelAsync(
        Guid noteId,
        Guid labelId,
        Guid userId,
        CancellationToken ct)
    {
        await using var connection =
            _connectionFactory.CreateConnection();

        await connection.OpenAsync(ct);

        await using var transaction =
            await connection.BeginTransactionAsync(ct);

        const string validateSql = """
            SELECT EXISTS
            (
                SELECT 1
                FROM "Notes" n
                INNER JOIN "Labels" l
                    ON l."Id" = @LabelId
                WHERE n."Id" = @NoteId
                  AND n."UserId" = @UserId
                  AND l."UserId" = @UserId
                  AND n."IsTrashed" = FALSE
            );
            """;

        await using (var validateCommand =
            new NpgsqlCommand(validateSql, connection, transaction))
        {
            validateCommand.Parameters.AddWithValue("NoteId", noteId);
            validateCommand.Parameters.AddWithValue("LabelId", labelId);
            validateCommand.Parameters.AddWithValue("UserId", userId);

            var valid = (bool)(await validateCommand.ExecuteScalarAsync(ct)
                ?? false);

            if (!valid)
            {
                await transaction.RollbackAsync(ct);
                return false;
            }
        }

        const string insertSql = """
            INSERT INTO "NoteLabels" ("NoteId", "LabelId")
            VALUES (@NoteId, @LabelId)
            ON CONFLICT ("NoteId", "LabelId") DO NOTHING;
            """;

        await using (var insertCommand =
            new NpgsqlCommand(insertSql, connection, transaction))
        {
            insertCommand.Parameters.AddWithValue("NoteId", noteId);
            insertCommand.Parameters.AddWithValue("LabelId", labelId);

            // Existing link is treated as success (idempotent operation).
            await insertCommand.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
        return true;
    }

    public async Task<bool> RemoveLabelAsync(
        Guid noteId,
        Guid labelId,
        Guid userId,
        CancellationToken ct)
    {
        const string sql = """
            DELETE FROM "NoteLabels" nl
            USING "Notes" n, "Labels" l
            WHERE nl."NoteId" = n."Id"
              AND nl."LabelId" = l."Id"
              AND nl."NoteId" = @NoteId
              AND nl."LabelId" = @LabelId
              AND n."UserId" = @UserId
              AND l."UserId" = @UserId;
            """;

        await using var connection =
            _connectionFactory.CreateConnection();

        await connection.OpenAsync(ct);

        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("NoteId", noteId);
        command.Parameters.AddWithValue("LabelId", labelId);
        command.Parameters.AddWithValue("UserId", userId);

        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    private static Note ReadNote(NpgsqlDataReader reader)
    {
        return new Note
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
            Title = reader.GetString(reader.GetOrdinal("Title")),
            Content = reader.GetString(reader.GetOrdinal("Content")),
            Color = reader.IsDBNull(reader.GetOrdinal("Color"))
                ? null
                : reader.GetString(reader.GetOrdinal("Color")),
            IsPinned = reader.GetBoolean(reader.GetOrdinal("IsPinned")),
            IsArchived = reader.GetBoolean(reader.GetOrdinal("IsArchived")),
            IsTrashed = reader.GetBoolean(reader.GetOrdinal("IsTrashed")),
            CreatedAtUtc = reader.GetDateTime(
                reader.GetOrdinal("CreatedAtUtc")),
            UpdatedAtUtc = reader.GetDateTime(
                reader.GetOrdinal("UpdatedAtUtc"))
        };
    }

    private static async Task LoadLabelsAsync(
        NpgsqlConnection connection,
        IReadOnlyCollection<Note> notes,
        CancellationToken ct)
    {
        var noteIds = notes.Select(n => n.Id).ToArray();

        const string sql = """
            SELECT
                nl."NoteId",
                l."Id" AS "LabelId",
                l."UserId" AS "LabelUserId",
                l."Name" AS "LabelName"
            FROM "NoteLabels" nl
            INNER JOIN "Labels" l
                ON l."Id" = nl."LabelId"
            WHERE nl."NoteId" = ANY(@NoteIds);
            """;

        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue(
            "NoteIds", NpgsqlDbType.Array | NpgsqlDbType.Uuid, noteIds);

        var notesById = notes.ToDictionary(n => n.Id);

        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var noteId = reader.GetGuid(reader.GetOrdinal("NoteId"));

            if (!notesById.TryGetValue(noteId, out var note))
            {
                continue;
            }

            note.NoteLabels.Add(new NoteLabel
            {
                NoteId = noteId,
                LabelId = reader.GetGuid(reader.GetOrdinal("LabelId")),
                Label = new Label
                {
                    Id = reader.GetGuid(reader.GetOrdinal("LabelId")),
                    UserId = reader.GetGuid(
                        reader.GetOrdinal("LabelUserId")),
                    Name = reader.GetString(
                        reader.GetOrdinal("LabelName"))
                }
            });
        }
    }

    private static async Task LoadCollaboratorsAsync(
        NpgsqlConnection connection,
        IReadOnlyCollection<Note> notes,
        CancellationToken ct)
    {
        var noteIds = notes.Select(n => n.Id).ToArray();

        const string sql = """
            SELECT "Id", "NoteId", "CollaboratorEmail", "CreatedAtUtc"
            FROM "Collaborators"
            WHERE "NoteId" = ANY(@NoteIds)
            ORDER BY "CreatedAtUtc" ASC;
            """;

        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue(
            "NoteIds", NpgsqlDbType.Array | NpgsqlDbType.Uuid, noteIds);

        var notesById = notes.ToDictionary(n => n.Id);

        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var noteId = reader.GetGuid(reader.GetOrdinal("NoteId"));

            if (!notesById.TryGetValue(noteId, out var note))
            {
                continue;
            }

            note.Collaborators.Add(new Collaborator
            {
                Id = reader.GetGuid(reader.GetOrdinal("Id")),
                NoteId = noteId,
                CollaboratorEmail = reader.GetString(reader.GetOrdinal("CollaboratorEmail")),
                CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"))
            });
        }
    }
}