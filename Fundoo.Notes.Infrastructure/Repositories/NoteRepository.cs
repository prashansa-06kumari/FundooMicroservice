using Fundoo.Notes.Application.Interfaces;
using Fundoo.Notes.Domain.Common;
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
        const string sql = """
            SELECT n.*
            FROM "Notes" n
            WHERE n."Id" = @Id
              AND (
                  n."UserId" = @UserId
                  OR (@UserEmail IS NOT NULL AND EXISTS (
                      SELECT 1 FROM "Collaborators" c
                      WHERE c."NoteId" = n."Id"
                        AND LOWER(c."CollaboratorEmail") = LOWER(@UserEmail)
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
            command.Parameters.AddWithValue("UserEmail", (object?)userEmail?.Trim().ToLowerInvariant() ?? DBNull.Value);

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
        CancellationToken ct)
    {
        const string sql = """
            SELECT *
            FROM "Notes"
            WHERE "UserId" = @UserId
              AND "IsArchived" = FALSE
              AND "IsTrashed" = FALSE
            ORDER BY "IsPinned" DESC, "UpdatedAtUtc" DESC;
            """;

        return await GetNotesAsync(sql, userId, ct);
    }

    public async Task<PagedResult<Note>> GetPagedAsync(
        Guid userId,
        NoteFilterParams filter,
        CancellationToken ct)
    {
        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var whereClauses = new List<string> { @"n.""UserId"" = @UserId" };
        var parameters = new List<NpgsqlParameter>
        {
            new("UserId", userId)
        };

        if (filter.IsArchived.HasValue)
        {
            whereClauses.Add(@"n.""IsArchived"" = @IsArchived");
            parameters.Add(new("IsArchived", filter.IsArchived.Value));
        }

        if (filter.IsTrashed.HasValue)
        {
            whereClauses.Add(@"n.""IsTrashed"" = @IsTrashed");
            parameters.Add(new("IsTrashed", filter.IsTrashed.Value));
        }

        if (!string.IsNullOrWhiteSpace(filter.Color))
        {
            whereClauses.Add(@"LOWER(n.""Color"") = @Color");
            parameters.Add(new("Color", filter.Color.Trim().ToLowerInvariant()));
        }

        // Full search across Title and Content
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            whereClauses.Add(@"(LOWER(n.""Title"") LIKE @Query OR LOWER(n.""Content"") LIKE @Query)");
            parameters.Add(new("Query", $"%{filter.Query.Trim().ToLowerInvariant()}%"));
        }

        // Specific Title search
        if (!string.IsNullOrWhiteSpace(filter.Title))
        {
            whereClauses.Add(@"LOWER(n.""Title"") LIKE @Title");
            parameters.Add(new("Title", $"%{filter.Title.Trim().ToLowerInvariant()}%"));
        }

        // Specific Content search
        if (!string.IsNullOrWhiteSpace(filter.Content))
        {
            whereClauses.Add(@"LOWER(n.""Content"") LIKE @Content");
            parameters.Add(new("Content", $"%{filter.Content.Trim().ToLowerInvariant()}%"));
        }

        // Filter by Label Name
        if (!string.IsNullOrWhiteSpace(filter.Label))
        {
            whereClauses.Add(@"EXISTS (
                SELECT 1 FROM ""NoteLabels"" nl
                INNER JOIN ""Labels"" l ON l.""Id"" = nl.""LabelId""
                WHERE nl.""NoteId"" = n.""Id""
                  AND LOWER(l.""Name"") = @LabelName
            )");
            parameters.Add(new("LabelName", filter.Label.Trim().ToLowerInvariant()));
        }

        // Filter by Label ID
        if (filter.LabelId.HasValue)
        {
            whereClauses.Add(@"EXISTS (
                SELECT 1 FROM ""NoteLabels"" nl
                WHERE nl.""NoteId"" = n.""Id""
                  AND nl.""LabelId"" = @LabelId
            )");
            parameters.Add(new("LabelId", filter.LabelId.Value));
        }

        var whereSql = string.Join(" AND ", whereClauses);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        // 1. Get Total Count
        var countSql = $@"SELECT COUNT(*) FROM ""Notes"" n WHERE {whereSql};";
        int totalCount;
        await using (var countCmd = new NpgsqlCommand(countSql, connection))
        {
            foreach (var p in parameters) countCmd.Parameters.Add(p.Clone());
            var countResult = await countCmd.ExecuteScalarAsync(ct);
            totalCount = Convert.ToInt32(countResult);
        }

        if (totalCount == 0)
        {
            return PagedResult<Note>.Create(
                Array.Empty<Note>(), 0, pageNumber, pageSize, "No notes found matching your criteria.");
        }

        // 2. Fetch Paged Notes
        var offset = (pageNumber - 1) * pageSize;
        var dataSql = $@"
            SELECT n.*
            FROM ""Notes"" n
            WHERE {whereSql}
            ORDER BY n.""IsPinned"" DESC, n.""UpdatedAtUtc"" DESC
            LIMIT @Limit OFFSET @Offset;";

        var notes = new List<Note>();
        await using (var dataCmd = new NpgsqlCommand(dataSql, connection))
        {
            foreach (var p in parameters) dataCmd.Parameters.Add(p.Clone());
            dataCmd.Parameters.Add(new("Limit", pageSize));
            dataCmd.Parameters.Add(new("Offset", offset));

            await using var reader = await dataCmd.ExecuteReaderAsync(ct);
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

        var message = notes.Count == 0 ? "No notes available on this page." : null;
        return PagedResult<Note>.Create(notes, totalCount, pageNumber, pageSize, message);
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

        return await GetNotesAsync(sql, userId, ct);
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

        return await GetNotesAsync(sql, userId, ct);
    }

    private async Task<List<Note>> GetNotesAsync(
        string sql,
        Guid userId,
        CancellationToken ct)
    {
        await using var connection =
            _connectionFactory.CreateConnection();

        await connection.OpenAsync(ct);

        var notes = new List<Note>();

        await using (var command = new NpgsqlCommand(sql, connection))
        {
            command.Parameters.AddWithValue("UserId", userId);

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
                      WHERE c."NoteId" = n."Id"
                        AND LOWER(c."CollaboratorEmail") = LOWER(@UserEmail)
                  ))
              );
            """;

        await using var connection =
            _connectionFactory.CreateConnection();

        await connection.OpenAsync(ct);

        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("Id", note.Id);
        command.Parameters.AddWithValue("UserId", note.UserId);
        command.Parameters.AddWithValue("UserEmail", (object?)userEmail?.Trim().ToLowerInvariant() ?? DBNull.Value);
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