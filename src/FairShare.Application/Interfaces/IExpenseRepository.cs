using FairShare.Domain.Entities;

namespace FairShare.Application.Interfaces;

/// <summary>Persistence contract for <see cref="Expense"/> aggregates including payers and splits.</summary>
public interface IExpenseRepository
{
    /// <summary>Retrieves a non-deleted expense by its primary key, including payers and splits.</summary>
    Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns all non-deleted expenses for a group, including their payers.</summary>
    Task<IReadOnlyList<Expense>> GetByGroupIdAsync(Guid groupId, CancellationToken ct = default);

    /// <summary>Persists a new expense along with its payer and split records.</summary>
    Task AddAsync(Expense expense, CancellationToken ct = default);

    /// <summary>Marks the expense as modified.</summary>
    void Update(Expense expense);

    /// <summary>Removes all <see cref="ExpensePayer"/> records for an expense (used on update).</summary>
    void RemovePayers(IEnumerable<ExpensePayer> payers);

    /// <summary>Removes all <see cref="ExpenseSplit"/> records for an expense (used on update).</summary>
    void RemoveSplits(IEnumerable<ExpenseSplit> splits);

    /// <summary>Commits all pending changes to the database.</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
