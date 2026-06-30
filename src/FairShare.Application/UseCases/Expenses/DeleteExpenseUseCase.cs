using FairShare.Application.Interfaces;

namespace FairShare.Application.UseCases.Expenses;

/// <summary>Result codes for the delete-expense operation.</summary>
public enum DeleteExpenseResult { Success, GroupNotFound, ExpenseNotFound }

/// <summary>Soft-deletes an expense by setting <c>IsDeleted = true</c>.</summary>
public class DeleteExpenseUseCase
{
    private readonly IGroupRepository _groups;
    private readonly IExpenseRepository _expenses;

    public DeleteExpenseUseCase(IGroupRepository groups, IExpenseRepository expenses)
    {
        _groups   = groups;
        _expenses = expenses;
    }

    /// <summary>Executes the use case.</summary>
    public async Task<DeleteExpenseResult> ExecuteAsync(
        Guid guestToken,
        Guid expenseId,
        CancellationToken ct = default)
    {
        var group = await _groups.GetByGuestTokenAsync(guestToken, ct);
        if (group is null) return DeleteExpenseResult.GroupNotFound;

        var expense = await _expenses.GetByIdAsync(expenseId, ct);
        if (expense is null || expense.GroupId != group.Id)
            return DeleteExpenseResult.ExpenseNotFound;

        expense.IsDeleted = true;
        _expenses.Update(expense);
        await _expenses.SaveChangesAsync(ct);

        return DeleteExpenseResult.Success;
    }
}
