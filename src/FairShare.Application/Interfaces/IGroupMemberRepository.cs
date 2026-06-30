using FairShare.Domain.Entities;

namespace FairShare.Application.Interfaces;

/// <summary>Persistence contract for <see cref="GroupMember"/> records.</summary>
public interface IGroupMemberRepository
{
    /// <summary>Retrieves a member by its primary key.</summary>
    Task<GroupMember?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns all members belonging to a group.</summary>
    Task<IReadOnlyList<GroupMember>> GetByGroupIdAsync(Guid groupId, CancellationToken ct = default);

    /// <summary>Persists a new group member.</summary>
    Task AddAsync(GroupMember member, CancellationToken ct = default);

    /// <summary>Marks the member as modified.</summary>
    void Update(GroupMember member);

    /// <summary>Removes the member record from the database.</summary>
    void Remove(GroupMember member);

    /// <summary>
    /// Returns true when the member has at least one non-deleted expense record
    /// (either as a payer or split participant), preventing orphan deletion.
    /// </summary>
    Task<bool> HasExpensesAsync(Guid memberId, CancellationToken ct = default);

    /// <summary>Commits all pending changes to the database.</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
