using System.ComponentModel.DataAnnotations;

namespace Fundoo.Notes.Api.Contracts;

public class CreateNoteRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = "";

    [Required]
    [StringLength(10000)]
    public string Content { get; set; } = "";

    [StringLength(30)]
    public string? Color { get; set; }
}

public class UpdateNoteRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = "";

    [Required]
    [StringLength(10000)]
    public string Content { get; set; } = "";

    [StringLength(30)]
    public string? Color { get; set; }

    public bool IsPinned { get; set; }
}

public class CreateLabelRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = "";
}

public class UpdateLabelRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = "";
}

public class AddCollaboratorRequest
{
    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = "";
}

public class CreateCollaboratorStandaloneRequest
{
    [Required]
    public Guid NoteId { get; set; }

    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = "";
}

public class NoteQueryRequest
{
    public int? PageNumber { get; set; } = 1;
    public int? PageSize { get; set; } = 10;
    public string? Query { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? Label { get; set; }
    public Guid? LabelId { get; set; }
    public string? Color { get; set; }
    public bool? IsArchived { get; set; } = false;
    public bool? IsTrashed { get; set; } = false;
}
