using FairShare.Application.DTOs.Expenses;
using FluentValidation;

namespace FairShare.Application.Validators;

/// <summary>Validates <see cref="CreateExpenseRequest"/>.</summary>
public class CreateExpenseValidator : AbstractValidator<CreateExpenseRequest>
{
    public CreateExpenseValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(150).WithMessage("Title must not exceed 150 characters.");

        RuleFor(x => x.TotalAmount)
            .GreaterThan(0).WithMessage("Total amount must be greater than 0.")
            .PrecisionScale(18, 2, true).WithMessage("Total amount can have at most 2 decimal places.");

        RuleFor(x => x.Payers)
            .NotEmpty().WithMessage("At least one payer is required.");

        RuleFor(x => x.Payers)
            .Must((req, payers) => payers != null &&
                Math.Abs(payers.Sum(p => p.AmountPaid) - req.TotalAmount) < 0.01m)
            .WithMessage("Sum of payer amounts must equal the total amount.")
            .When(x => x.Payers is { Count: > 0 });

        RuleForEach(x => x.Payers).ChildRules(payer =>
        {
            payer.RuleFor(p => p.MemberId)
                .NotEmpty().WithMessage("Payer member ID is required.");
            payer.RuleFor(p => p.AmountPaid)
                .GreaterThan(0).WithMessage("Amount paid must be greater than 0.")
                .PrecisionScale(18, 2, true).WithMessage("Amount paid can have at most 2 decimal places.");
        });

        RuleFor(x => x.ParticipantMemberIds)
            .NotEmpty().WithMessage("At least one participant is required.")
            .When(x => x.ParticipantMemberIds is not null);
    }
}
