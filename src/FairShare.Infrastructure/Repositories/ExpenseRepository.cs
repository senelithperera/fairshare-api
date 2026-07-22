using FairShare.Application.Interfaces;
using FairShare.Domain.Entities;
using FairShare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FairShare.Infrastructure.Repositories;

/// <summary>EF Core implementation of <see cref="IExpenseRepository"/>.</summary>
public class ExpenseRepository : IExpenseRepository
{
    private readonly FairShareDbContext _db;

    public ExpenseRepository(FairShareDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Expenses
            .Include(e => e.Payers).ThenInclude(p => p.Member)
            .Include(e => e.Splits).ThenInclude(s => s.Member)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Expense>> GetByGroupIdAsync(Guid groupId, CancellationToken ct = default)
        => await _db.Expenses
            .Include(e => e.Payers).ThenInclude(p => p.Member)
            .Include(e => e.Splits)
            .Where(e => e.GroupId == groupId)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task AddAsync(Expense expense, CancellationToken ct = default)
        => await _db.Expenses.AddAsync(expense, ct);

    /// <inheritdoc/>
    public void Update(Expense expense) => _db.Expenses.Update(expense);

    /// <inheritdoc/>
    public void RemovePayers(IEnumerable<ExpensePayer> payers) => _db.ExpensePayers.RemoveRange(payers);

    /// <inheritdoc/>
    public void RemoveSplits(IEnumerable<ExpenseSplit> splits) => _db.ExpenseSplits.RemoveRange(splits);

    /// <inheritdoc/>
    public void AddPayers(IEnumerable<ExpensePayer> payers) => _db.ExpensePayers.AddRange(payers);

    /// <inheritdoc/>
    public void AddSplits(IEnumerable<ExpenseSplit> splits) => _db.ExpenseSplits.AddRange(splits);

    /// <inheritdoc/>
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
