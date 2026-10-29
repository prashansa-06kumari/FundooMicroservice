using System.ComponentModel.DataAnnotations;

namespace Fundoo.Notes.Api.Contracts;

public class CreateNoteRequest
{
    [Required, MaxLength(200)] public string Title { get; set; } = "";
    [MaxLength(20000)] public string Content { get; set; } = "";
    [MaxLength(50)] public string? Color { get; set; }
}

public class UpdateNoteRequest
{
    [Required, MaxLength(200)] public string Title { get; set; } = "";
    [MaxLength(20000)] public string Content { get; set; } = "";
    [MaxLength(50)] public string? Color { get; set; }
    public bool IsPinned { get; set; }
}