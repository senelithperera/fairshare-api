using FairShare.Application.Interfaces;
using FairShare.Domain.Entities;
using FairShare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FairShare.Infrastructure.Repositories;

/// <summary>EF Core implementation of <see cref="IGroupMemberRepository"/>.</summary>
public class GroupMemberRepository : IGroupMemberRepository
{
    private readonly FairShareDbContext _db;

    public GroupMemberRepository(FairShareDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<GroupMember?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.GroupMembers
            .Include(m => m.User)
            .Include(m => m.PayerRecords)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<GroupMember>> GetByGroupIdAsync(Guid groupId, CancellationToken ct = default)
        => await _db.GroupMembers
            .Include(m => m.User)
            .Include(m => m.PayerRecords)
            .Where(m => m.GroupId == groupId)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task AddAsync(GroupMember member, CancellationToken ct = default)
        => await _db.GroupMembers.AddAsync(member, ct);

    /// <inheritdoc/>
    public void Update(GroupMember member) => _db.GroupMembers.Update(member);

    /// <inheritdoc/>
    public void Remove(GroupMember member) => _db.GroupMembers.Remove(member);

    /// <inheritdoc/>
    public async Task<bool> HasExpensesAsync(Guid memberId, CancellationToken ct = default)
        => await _db.ExpensePayers.AnyAsync(p => p.MemberId == memberId, ct)
        || await _db.ExpenseSplits.AnyAsync(s => s.MemberId == memberId, ct);

    /// <inheritdoc/>
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
