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
    }
}
