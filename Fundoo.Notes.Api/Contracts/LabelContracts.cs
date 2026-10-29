using System.ComponentModel.DataAnnotations;

namespace Fundoo.Notes.Api.Contracts;

public class LabelRequest
{
    [Required, MaxLength(100)] public string Name { get; set; } = "";
}