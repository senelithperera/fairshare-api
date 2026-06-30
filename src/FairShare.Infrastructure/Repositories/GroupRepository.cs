using FairShare.Application.Interfaces;
using FairShare.Domain.Entities;
using FairShare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FairShare.Infrastructure.Repositories;

/// <summary>EF Core implementation of <see cref="IGroupRepository"/>.</summary>
public class GroupRepository : IGroupRepository
{
    private readonly FairShareDbContext _db;

    public GroupRepository(FairShareDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<Group?> GetByGuestTokenAsync(Guid guestToken, CancellationToken ct = default)
        => await _db.Groups
            .Include(g => g.Members).ThenInclude(m => m.User)
            .Include(g => g.Members).ThenInclude(m => m.PayerRecords)
            .Include(g => g.Expenses).ThenInclude(e => e.Payers).ThenInclude(p => p.Member)
            .Include(g => g.Expenses).ThenInclude(e => e.Splits)
            .IgnoreQueryFilters()                     // we need archived check on the caller side if needed
            .FirstOrDefaultAsync(g => g.GuestToken == guestToken && !g.IsArchived, ct);

    /// <inheritdoc/>
    public async Task<Group?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == id && !g.IsArchived, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Group>> GetAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        // Groups where user is owner OR has a GroupMember record linked to them
        return await _db.Groups
            .Where(g => g.CreatedByUserId == userId
                || g.Members.Any(m => m.UserId == userId))
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task AddAsync(Group group, CancellationToken ct = default)
        => await _db.Groups.AddAsync(group, ct);

    /// <inheritdoc/>
    public void Update(Group group) => _db.Groups.Update(group);

    /// <inheritdoc/>
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
