using Fundoo.Notes.Application.Labels;

namespace Fundoo.Notes.Application.Abstractions;

public interface ILabelService
{
    Task<List<LabelDto>> ListAsync(Guid userId, CancellationToken ct = default);
    Task<LabelOutcome> CreateAsync(Guid userId, string name, CancellationToken ct = default);
    Task<LabelOutcome> RenameAsync(Guid userId, Guid id, string name, CancellationToken ct = default);
    Task<LabelResult> DeleteAsync(Guid userId, Guid id, CancellationToken ct = default);
    Task<LabelResult> AttachAsync(Guid userId, Guid noteId, Guid labelId, CancellationToken ct = default);
    Task<LabelResult> DetachAsync(Guid userId, Guid noteId, Guid labelId, CancellationToken ct = default);
}