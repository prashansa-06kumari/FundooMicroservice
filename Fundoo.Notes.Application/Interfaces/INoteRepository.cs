using Fundoo.Notes.Domain.Entities;

namespace Fundoo.Notes.Application.Interfaces;

public interface INoteRepository
{
    Task<Note> CreateAsync(Note note, CancellationToken ct);
    Task<Note?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct);
    Task<List<Note>> GetAllAsync(Guid userId, CancellationToken ct);
    Task<List<Note>> GetArchivedAsync(Guid userId, CancellationToken ct);
    Task<List<Note>> GetTrashAsync(Guid userId, CancellationToken ct);
    Task<bool> UpdateAsync(Note note, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct);
    Task<bool> SetArchivedAsync(Guid id, Guid userId, bool archived, CancellationToken ct);
    Task<bool> SetTrashedAsync(Guid id, Guid userId, bool trashed, CancellationToken ct);
    Task<bool> AddLabelAsync(Guid noteId, Guid labelId, Guid userId, CancellationToken ct);
    Task<bool> RemoveLabelAsync(Guid noteId, Guid labelId, Guid userId, CancellationToken ct);
}