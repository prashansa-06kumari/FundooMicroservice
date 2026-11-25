namespace Fundoo.Notes.Domain.Common;

public class NoteFilterParams
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Query { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? Label { get; set; }
    public Guid? LabelId { get; set; }
    public bool? IsArchived { get; set; } = false;
    public bool? IsTrashed { get; set; } = false;
    public string? Color { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = true;
}
