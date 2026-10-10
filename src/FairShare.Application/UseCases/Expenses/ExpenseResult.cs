using FairShare.Application.DTOs.Expenses;

namespace FairShare.Application.UseCases.Expenses;

/// <summary>Result codes for the create/update expense operations.</summary>
public enum ExpenseResultStatus
{
    Success,
    NotFound,
    PayerNotInGroup,
    ParticipantNotInGroup
}

/// <summary>Outcome of a create/update expense operation; <see cref="Expense"/> is set on success.</summary>
public record ExpenseResult(ExpenseResultStatus Status, ExpenseResponse? Expense = null)
{
    public static ExpenseResult Success(ExpenseResponse expense) => new(ExpenseResultStatus.Success, expense);
    public static readonly ExpenseResult NotFound              = new(ExpenseResultStatus.NotFound);
    public static readonly ExpenseResult PayerNotInGroup       = new(ExpenseResultStatus.PayerNotInGroup);
    public static readonly ExpenseResult ParticipantNotInGroup = new(ExpenseResultStatus.ParticipantNotInGroup);
}
