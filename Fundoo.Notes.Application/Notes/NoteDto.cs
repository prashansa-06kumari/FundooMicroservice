using Fundoo.Notes.Application.Labels;
using Fundoo.Notes.Domain.Entities;

namespace Fundoo.Notes.Application.Notes;

public record NoteDto(
    Guid Id, string Title, string Content, string? Color,
    bool IsPinned, bool IsArchived, bool IsTrashed,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc,
    List<LabelDto> Labels)
{
    public static NoteDto From(Note n) => new(
        n.Id, n.Title, n.Content, n.Color,
        n.IsPinned, n.IsArchived, n.IsTrashed,
        n.CreatedAtUtc, n.UpdatedAtUtc,
        n.NoteLabels.Select(nl => new LabelDto(nl.Label.Id, nl.Label.Name)).ToList());
}

public enum DeleteResult { Deleted, NotFound, NotTrashed }