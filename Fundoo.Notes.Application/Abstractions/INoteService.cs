using Fundoo.Notes.Application.Notes;

namespace Fundoo.Notes.Application.Abstractions;

public interface INoteService
{
    Task<NoteDto> CreateAsync(Guid userId, string title, string content, string? color, CancellationToken ct = default);
    Task<List<NoteDto>> ListAsync(Guid userId, NoteView view, CancellationToken ct = default);
    Task<NoteDto?> GetAsync(Guid userId, Guid id, CancellationToken ct = default);
    Task<NoteDto?> UpdateAsync(Guid userId, Guid id, string title, string content, string? color, bool isPinned, CancellationToken ct = default);
    Task<NoteDto?> SetArchivedAsync(Guid userId, Guid id, bool archived, CancellationToken ct = default);
    Task<NoteDto?> SetTrashedAsync(Guid userId, Guid id, bool trashed, CancellationToken ct = default);
    Task<DeleteResult> DeleteAsync(Guid userId, Guid id, CancellationToken ct = default);
}