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