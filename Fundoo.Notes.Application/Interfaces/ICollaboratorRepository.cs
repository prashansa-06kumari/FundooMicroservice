using Fundoo.Notes.Domain.Entities;

namespace Fundoo.Notes.Application.Interfaces;

public interface ICollaboratorRepository
{
    Task<Collaborator> AddCollaboratorAsync(Guid noteId, string email, Guid userId, CancellationToken ct);
    Task<bool> RemoveCollaboratorAsync(Guid noteId, Guid collaboratorId, Guid userId, CancellationToken ct);
    Task<bool> RemoveCollaboratorByEmailAsync(Guid noteId, string email, Guid userId, CancellationToken ct);
    Task<List<Collaborator>> GetCollaboratorsByNoteIdAsync(Guid noteId, Guid userId, CancellationToken ct);
    Task<List<Note>> GetCollaboratedNotesAsync(Guid userId, string userEmail, CancellationToken ct);
}
