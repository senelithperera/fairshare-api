using FairShare.Application.DTOs.Groups;
using FluentValidation;

namespace FairShare.Application.Validators;

/// <summary>Validates <see cref="CreateGroupRequest"/>.</summary>
public class CreateGroupValidator : AbstractValidator<CreateGroupRequest>
{
    public CreateGroupValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Group name is required.")
            .MaximumLength(100).WithMessage("Group name must not exceed 100 characters.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required.")
            .MaximumLength(10).WithMessage("Currency code must not exceed 10 characters.");
    }
}
