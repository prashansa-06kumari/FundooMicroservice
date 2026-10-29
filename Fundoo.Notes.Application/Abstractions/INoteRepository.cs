using Fundoo.Notes.Domain.Entities;

namespace Fundoo.Notes.Application.Abstractions;

public interface INoteRepository
{
    Task<Note?> GetOwnedAsync(Guid userId, Guid id, CancellationToken ct = default);
    Task<List<Note>> ListAsync(Guid userId, NoteView view, CancellationToken ct = default);
    Task AddAsync(Note note, CancellationToken ct = default);
    Task UpdateAsync(Note note, CancellationToken ct = default);
    Task DeleteAsync(Note note, CancellationToken ct = default);
}