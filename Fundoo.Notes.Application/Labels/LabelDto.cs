using Fundoo.Notes.Domain.Entities;

namespace Fundoo.Notes.Application.Labels;

public record LabelDto(Guid Id, string Name)
{
    public static LabelDto From(Label l) => new(l.Id, l.Name);
}

public enum LabelResult { Ok, NotFound, Duplicate }

public record LabelOutcome(LabelResult Result, LabelDto? Label = null);