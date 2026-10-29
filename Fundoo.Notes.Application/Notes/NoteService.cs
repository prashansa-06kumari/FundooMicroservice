using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Domain.Entities;

namespace Fundoo.Notes.Application.Notes;

public class NoteService(INoteRepository repo) : INoteService
{
    public async Task<NoteDto> CreateAsync(Guid userId, string title, string content, string? color, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var note = new Note
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title.Trim(),
            Content = content,
            Color = color,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        await repo.AddAsync(note, ct);
        return NoteDto.From(note);
    }

    public async Task<List<NoteDto>> ListAsync(Guid userId, NoteView view, CancellationToken ct = default) =>
        (await repo.ListAsync(userId, view, ct)).Select(NoteDto.From).ToList();

    public async Task<NoteDto?> GetAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var note = await repo.GetOwnedAsync(userId, id, ct);
        return note is null ? null : NoteDto.From(note);
    }

    public Task<NoteDto?> UpdateAsync(Guid userId, Guid id, string title, string content, string? color, bool isPinned, CancellationToken ct = default) =>
        MutateAsync(userId, id, n =>
        {
            n.Title = title.Trim();
            n.Content = content;
            n.Color = color;
            n.IsPinned = isPinned;
        }, ct);

    public Task<NoteDto?> SetArchivedAsync(Guid userId, Guid id, bool archived, CancellationToken ct = default) =>
        MutateAsync(userId, id, n =>
        {
            n.IsArchived = archived;
            if (archived) n.IsPinned = false;
        }, ct);

    public Task<NoteDto?> SetTrashedAsync(Guid userId, Guid id, bool trashed, CancellationToken ct = default) =>
        MutateAsync(userId, id, n =>
        {
            n.IsTrashed = trashed;
            if (trashed) n.IsPinned = false;
        }, ct);

    public async Task<DeleteResult> DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var note = await repo.GetOwnedAsync(userId, id, ct);
        if (note is null) return DeleteResult.NotFound;
        if (!note.IsTrashed) return DeleteResult.NotTrashed;

        await repo.DeleteAsync(note, ct);
        return DeleteResult.Deleted;
    }

    private async Task<NoteDto?> MutateAsync(Guid userId, Guid id, Action<Note> change, CancellationToken ct)
    {
        var note = await repo.GetOwnedAsync(userId, id, ct);
        if (note is null) return null;

        change(note);
        note.UpdatedAtUtc = DateTime.UtcNow;
        await repo.UpdateAsync(note, ct);
        return NoteDto.From(note);
    }
}