using FairShare.Application.DTOs.Expenses;
using FluentValidation;

namespace FairShare.Application.Validators;

/// <summary>Validates <see cref="UpdateExpenseRequest"/>.</summary>
public class UpdateExpenseValidator : AbstractValidator<UpdateExpenseRequest>
{
    public UpdateExpenseValidator()
    {
        RuleFor(x => x.Title)
            .MaximumLength(150).WithMessage("Title must not exceed 150 characters.")
            .When(x => x.Title is not null);

        RuleFor(x => x.TotalAmount)
            .GreaterThan(0).WithMessage("Total amount must be greater than 0.")
            .PrecisionScale(18, 2, true).WithMessage("Total amount can have at most 2 decimal places.")
            .When(x => x.TotalAmount.HasValue);

        RuleFor(x => x.Payers)
            .NotEmpty().WithMessage("At least one payer is required.")
            .When(x => x.Payers is not null);

        // When both TotalAmount and Payers are provided, their sums must match
        RuleFor(x => x.Payers)
            .Must((req, payers) => payers == null || req.TotalAmount == null ||
                Math.Abs(payers.Sum(p => p.AmountPaid) - req.TotalAmount.Value) < 0.01m)
            .WithMessage("Sum of payer amounts must equal the total amount.")
            .When(x => x.Payers is { Count: > 0 } && x.TotalAmount.HasValue);

        RuleFor(x => x.Payers!)
            .Must(PayerRules.HaveDistinctMembers).WithMessage("Each member can only be listed once as a payer.")
            .When(x => x.Payers is { Count: > 0 });

        RuleForEach(x => x.Payers).ChildRules(payer =>
        {
            payer.RuleFor(p => p.MemberId).Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("Payer member ID is required.")
                .Must(PayerRules.BeAGuid).WithMessage("Payer member ID must be a valid ID.");
            payer.RuleFor(p => p.AmountPaid)
                .GreaterThan(0).WithMessage("Amount paid must be greater than 0.")
                .PrecisionScale(18, 2, true).WithMessage("Amount paid can have at most 2 decimal places.");
        }).When(x => x.Payers is not null);

        RuleFor(x => x.ParticipantMemberIds)
            .NotEmpty().WithMessage("At least one participant is required.")
            .When(x => x.ParticipantMemberIds is not null);
    }
}
