using FairShare.Application.DTOs.Groups;
using FluentValidation;

namespace FairShare.Application.Validators;

/// <summary>Validates <see cref="AddMemberRequest"/>.</summary>
public class AddMemberValidator : AbstractValidator<AddMemberRequest>
{
    public AddMemberValidator()
    {
        RuleFor(x => x.GuestName)
            .NotEmpty().WithMessage("Guest name is required.")
            .MaximumLength(80).WithMessage("Guest name must not exceed 80 characters.");

        RuleFor(x => x.BudgetCap)
            .GreaterThan(0).WithMessage("Budget cap must be greater than 0.")
            .When(x => x.BudgetCap.HasValue);
    }
}
