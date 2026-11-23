using FluentValidation;
using Fundoo.Notes.Api.Contracts;

namespace Fundoo.Notes.Api.Validators;

public class CreateNoteRequestValidator : AbstractValidator<CreateNoteRequest>
{
    public CreateNoteRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title cannot exceed 200 characters.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Content is required.")
            .MaximumLength(10000).WithMessage("Content cannot exceed 10,000 characters.");

        RuleFor(x => x.Color)
            .MaximumLength(30).WithMessage("Color cannot exceed 30 characters.");
    }
}

public class UpdateNoteRequestValidator : AbstractValidator<UpdateNoteRequest>
{
    public UpdateNoteRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title cannot exceed 200 characters.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Content is required.")
            .MaximumLength(10000).WithMessage("Content cannot exceed 10,000 characters.");

        RuleFor(x => x.Color)
            .MaximumLength(30).WithMessage("Color cannot exceed 30 characters.");
    }
}

public class CreateLabelRequestValidator : AbstractValidator<CreateLabelRequest>
{
    public CreateLabelRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Label name is required.")
            .MaximumLength(100).WithMessage("Label name cannot exceed 100 characters.");
    }
}
