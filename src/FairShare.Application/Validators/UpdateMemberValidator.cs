using FairShare.Application.DTOs.Groups;
using FluentValidation;

namespace FairShare.Application.Validators;

/// <summary>Validates <see cref="UpdateMemberRequest"/>.</summary>
public class UpdateMemberValidator : AbstractValidator<UpdateMemberRequest>
{
    public UpdateMemberValidator()
    {
        RuleFor(x => x.GuestName)
            .MaximumLength(80).WithMessage("Guest name must not exceed 80 characters.")
            .When(x => x.GuestName is not null);

        RuleFor(x => x.BudgetCap)
            .GreaterThan(0).WithMessage("Budget cap must be greater than 0.")
            .When(x => x.BudgetCap.HasValue);
    }
}
