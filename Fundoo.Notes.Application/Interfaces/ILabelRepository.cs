using Fundoo.Notes.Domain.Entities;

namespace Fundoo.Notes.Application.Interfaces;

public interface ILabelRepository
{
    Task<Label?> CreateAsync(Label label, CancellationToken ct);
    Task<List<Label>> GetAllAsync(Guid userId, CancellationToken ct);
    Task<Label?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct);
    Task<bool> UpdateAsync(Label label, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct);
}