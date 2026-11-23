namespace Fundoo.Notes.Domain.Entities;

public class Label
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";

    public List<NoteLabel> NoteLabels { get; set; } = new();
}