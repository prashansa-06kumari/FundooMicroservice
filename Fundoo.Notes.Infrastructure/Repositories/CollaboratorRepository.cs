using Fundoo.Notes.Application.Interfaces;
using Fundoo.Notes.Domain.Entities;
using Fundoo.Notes.Infrastructure.Data;
using Npgsql;
using NpgsqlTypes;

namespace Fundoo.Notes.Infrastructure.Repositories;

public class CollaboratorRepository : ICollaboratorRepository
{
    private readonly NotesConnectionFactory _connectionFactory;

    public CollaboratorRepository(NotesConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Collaborator> AddCollaboratorAsync(
        Guid noteId,
        string email,
        Guid userId,
        CancellationToken ct)
    {
        email = email.Trim().ToLowerInvariant();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        // Verify note exists and belongs to requesting user, and is not trashed
        const string checkSql = """
            SELECT "Id"
            FROM "Notes"
            WHERE "Id" = @NoteId AND "UserId" = @UserId AND "IsTrashed" = FALSE;
            """;

        await using (var checkCmd = new NpgsqlCommand(checkSql, connection))
        {
            checkCmd.Parameters.AddWithValue("NoteId", noteId);
            checkCmd.Parameters.AddWithValue("UserId", userId);

            var existing = await checkCmd.ExecuteScalarAsync(ct);
            if (existing == null)
            {
                throw new KeyNotFoundException("Note not found, is trashed, or you are not authorized to add collaborators.");
            }
        }

        // Insert collaborator or return existing if already collaborated
        const string insertSql = """
            INSERT INTO "Collaborators" ("Id", "NoteId", "CollaboratorEmail", "CreatedAtUtc")
            VALUES (@Id, @NoteId, @CollaboratorEmail, @CreatedAtUtc)
            ON CONFLICT ("NoteId", "CollaboratorEmail")
            DO UPDATE SET "CollaboratorEmail" = EXCLUDED."CollaboratorEmail"
            RETURNING "Id", "NoteId", "CollaboratorEmail", "CreatedAtUtc";
            """;

        var newId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var insertCmd = new NpgsqlCommand(insertSql, connection);
        insertCmd.Parameters.AddWithValue("Id", newId);
        insertCmd.Parameters.AddWithValue("NoteId", noteId);
        insertCmd.Parameters.AddWithValue("CollaboratorEmail", email);
        insertCmd.Parameters.AddWithValue("CreatedAtUtc", now);

        await using var reader = await insertCmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return ReadCollaborator(reader);
        }

        return new Collaborator
        {
            Id = newId,
            NoteId = noteId,
            CollaboratorEmail = email,
            CreatedAtUtc = now
        };
    }

    public async Task<bool> RemoveCollaboratorAsync(
        Guid noteId,
        Guid collaboratorId,
        Guid userId,
        CancellationToken ct)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        const string sql = """
            DELETE FROM "Collaborators" c
            USING "Notes" n
            WHERE c."NoteId" = n."Id"
              AND c."Id" = @CollaboratorId
              AND (@NoteId = '00000000-0000-0000-0000-000000000000'::uuid OR c."NoteId" = @NoteId)
              AND n."UserId" = @UserId;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("CollaboratorId", collaboratorId);
        command.Parameters.AddWithValue("NoteId", noteId);
        command.Parameters.AddWithValue("UserId", userId);

        var rows = await command.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<bool> RemoveCollaboratorByEmailAsync(
        Guid noteId,
        string email,
        Guid userId,
        CancellationToken ct)
    {
        email = email.Trim().ToLowerInvariant();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        const string sql = """
            DELETE FROM "Collaborators" c
            USING "Notes" n
            WHERE c."NoteId" = n."Id"
              AND LOWER(c."CollaboratorEmail") = @CollaboratorEmail
              AND c."NoteId" = @NoteId
              AND n."UserId" = @UserId;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("CollaboratorEmail", email);
        command.Parameters.AddWithValue("NoteId", noteId);
        command.Parameters.AddWithValue("UserId", userId);

        var rows = await command.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<List<Collaborator>> GetCollaboratorsByNoteIdAsync(
        Guid noteId,
        Guid userId,
        CancellationToken ct)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        // Check user access: must be owner OR a collaborator on this note
        const string checkAccessSql = """
            SELECT 1 FROM "Notes" WHERE "Id" = @NoteId AND "UserId" = @UserId
            UNION
            SELECT 1 FROM "Collaborators" c
            INNER JOIN "Notes" n ON n."Id" = c."NoteId"
            WHERE c."NoteId" = @NoteId;
            """;

        await using (var checkCmd = new NpgsqlCommand(checkAccessSql, connection))
        {
            checkCmd.Parameters.AddWithValue("NoteId", noteId);
            checkCmd.Parameters.AddWithValue("UserId", userId);
            var access = await checkCmd.ExecuteScalarAsync(ct);
            if (access == null)
            {
                return new List<Collaborator>();
            }
        }

        const string sql = """
            SELECT "Id", "NoteId", "CollaboratorEmail", "CreatedAtUtc"
            FROM "Collaborators"
            WHERE "NoteId" = @NoteId
            ORDER BY "CreatedAtUtc" ASC;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("NoteId", noteId);

        var list = new List<Collaborator>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadCollaborator(reader));
        }

        return list;
    }

    public async Task<List<Note>> GetCollaboratedNotesAsync(
        string userEmail,
        CancellationToken ct)
    {
        userEmail = userEmail.Trim().ToLowerInvariant();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        const string sql = """
            SELECT n.*
            FROM "Notes" n
            INNER JOIN "Collaborators" c ON n."Id" = c."NoteId"
            WHERE LOWER(c."CollaboratorEmail") = @UserEmail
              AND n."IsTrashed" = FALSE
            ORDER BY n."UpdatedAtUtc" DESC;
            """;

        var notes = new List<Note>();
        await using (var command = new NpgsqlCommand(sql, connection))
        {
            command.Parameters.AddWithValue("UserEmail", userEmail);

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                notes.Add(new Note
                {
                    Id = reader.GetGuid(reader.GetOrdinal("Id")),
                    UserId = reader.GetGuid(reader.GetOrdinal("UserId")),
                    Title = reader.GetString(reader.GetOrdinal("Title")),
                    Content = reader.GetString(reader.GetOrdinal("Content")),
                    Color = reader.IsDBNull(reader.GetOrdinal("Color")) ? null : reader.GetString(reader.GetOrdinal("Color")),
                    IsPinned = reader.GetBoolean(reader.GetOrdinal("IsPinned")),
                    IsArchived = reader.GetBoolean(reader.GetOrdinal("IsArchived")),
                    IsTrashed = reader.GetBoolean(reader.GetOrdinal("IsTrashed")),
                    CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc")),
                    UpdatedAtUtc = reader.GetDateTime(reader.GetOrdinal("UpdatedAtUtc"))
                });
            }
        }

        if (notes.Count > 0)
        {
            await LoadNoteDetailsAsync(connection, notes, ct);
        }

        return notes;
    }

    private static async Task LoadNoteDetailsAsync(
        NpgsqlConnection connection,
        IReadOnlyCollection<Note> notes,
        CancellationToken ct)
    {
        var noteIds = notes.Select(n => n.Id).ToArray();
        var notesById = notes.ToDictionary(n => n.Id);

        // Load labels
        const string labelSql = """
            SELECT
                nl."NoteId",
                l."Id" AS "LabelId",
                l."UserId" AS "LabelUserId",
                l."Name" AS "LabelName"
            FROM "NoteLabels" nl
            INNER JOIN "Labels" l ON l."Id" = nl."LabelId"
            WHERE nl."NoteId" = ANY(@NoteIds);
            """;

        await using (var cmd = new NpgsqlCommand(labelSql, connection))
        {
            cmd.Parameters.AddWithValue("NoteIds", NpgsqlDbType.Array | NpgsqlDbType.Uuid, noteIds);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var noteId = reader.GetGuid(reader.GetOrdinal("NoteId"));
                if (notesById.TryGetValue(noteId, out var note))
                {
                    note.NoteLabels.Add(new NoteLabel
                    {
                        NoteId = noteId,
                        LabelId = reader.GetGuid(reader.GetOrdinal("LabelId")),
                        Label = new Label
                        {
                            Id = reader.GetGuid(reader.GetOrdinal("LabelId")),
                            UserId = reader.GetGuid(reader.GetOrdinal("LabelUserId")),
                            Name = reader.GetString(reader.GetOrdinal("LabelName"))
                        }
                    });
                }
            }
        }

        // Load collaborators
        const string collabSql = """
            SELECT "Id", "NoteId", "CollaboratorEmail", "CreatedAtUtc"
            FROM "Collaborators"
            WHERE "NoteId" = ANY(@NoteIds)
            ORDER BY "CreatedAtUtc" ASC;
            """;

        await using (var cmd = new NpgsqlCommand(collabSql, connection))
        {
            cmd.Parameters.AddWithValue("NoteIds", NpgsqlDbType.Array | NpgsqlDbType.Uuid, noteIds);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var noteId = reader.GetGuid(reader.GetOrdinal("NoteId"));
                if (notesById.TryGetValue(noteId, out var note))
                {
                    note.Collaborators.Add(ReadCollaborator(reader));
                }
            }
        }
    }

    private static Collaborator ReadCollaborator(NpgsqlDataReader reader)
    {
        return new Collaborator
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            NoteId = reader.GetGuid(reader.GetOrdinal("NoteId")),
            CollaboratorEmail = reader.GetString(reader.GetOrdinal("CollaboratorEmail")),
            CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"))
        };
    }
}
