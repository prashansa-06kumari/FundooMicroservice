namespace Fundoo.Notes.Domain.Entities;

public class Label
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public List<NoteLabel> NoteLabels { get; set; } = new();
}