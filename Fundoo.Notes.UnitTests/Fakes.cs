using Fundoo.Notes.Application.Abstractions;
using Fundoo.Notes.Domain.Entities;

namespace Fundoo.Notes.UnitTests;

public class FakeNoteRepository : INoteRepository
{
    public List<Note> Notes { get; } = new();

    public Task<Note?> GetOwnedAsync(Guid userId, Guid id, CancellationToken ct = default) =>
        Task.FromResult(Notes.FirstOrDefault(n => n.Id == id && n.UserId == userId));

    public Task<List<Note>> ListAsync(Guid userId, NoteView view, CancellationToken ct = default)
    {
        var q = Notes.Where(n => n.UserId == userId);
        q = view switch
        {
            NoteView.Active => q.Where(n => !n.IsArchived && !n.IsTrashed),
            NoteView.Archived => q.Where(n => n.IsArchived && !n.IsTrashed),
            NoteView.Trashed => q.Where(n => n.IsTrashed),
            _ => q
        };
        return Task.FromResult(q.ToList());
    }

    public Task AddAsync(Note note, CancellationToken ct = default)
    {
        Notes.Add(note);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Note note, CancellationToken ct = default) => Task.CompletedTask;

    public Task DeleteAsync(Note note, CancellationToken ct = default)
    {
        Notes.Remove(note);
        return Task.CompletedTask;
    }
}

public class FakeLabelRepository : ILabelRepository
{
    public List<Label> Labels { get; } = new();
    public List<NoteLabel> Links { get; } = new();

    public Task<List<Label>> ListAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(Labels.Where(l => l.UserId == userId).ToList());

    public Task<Label?> GetOwnedAsync(Guid userId, Guid id, CancellationToken ct = default) =>
        Task.FromResult(Labels.FirstOrDefault(l => l.Id == id && l.UserId == userId));

    public Task<bool> NameExistsAsync(Guid userId, string name, Guid? excludeId, CancellationToken ct = default) =>
        Task.FromResult(Labels.Any(l => l.UserId == userId
                                        && string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase)
                                        && (excludeId == null || l.Id != excludeId)));

    public Task AddAsync(Label label, CancellationToken ct = default)
    {
        Labels.Add(label);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Label label, CancellationToken ct = default) => Task.CompletedTask;

    public Task DeleteAsync(Label label, CancellationToken ct = default)
    {
        Labels.Remove(label);
        return Task.CompletedTask;
    }

    public Task<NoteLabel?> FindLinkAsync(Guid noteId, Guid labelId, CancellationToken ct = default) =>
        Task.FromResult(Links.FirstOrDefault(x => x.NoteId == noteId && x.LabelId == labelId));

    public Task AddLinkAsync(NoteLabel link, CancellationToken ct = default)
    {
        Links.Add(link);
        return Task.CompletedTask;
    }

    public Task RemoveLinkAsync(NoteLabel link, CancellationToken ct = default)
    {
        Links.Remove(link);
        return Task.CompletedTask;
    }
}