using FairShare.Domain.Entities;

namespace FairShare.Application.Interfaces;

/// <summary>Persistence contract for <see cref="Group"/> aggregates.</summary>
public interface IGroupRepository
{
    /// <summary>Retrieves a group by its guest token, including members and active expenses.</summary>
    Task<Group?> GetByGuestTokenAsync(Guid guestToken, CancellationToken ct = default);

    /// <summary>Retrieves a group by its primary key.</summary>
    Task<Group?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns all non-archived groups the user owns or belongs to.</summary>
    Task<IReadOnlyList<Group>> GetAllForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Persists a new group.</summary>
    Task AddAsync(Group group, CancellationToken ct = default);

    /// <summary>Marks the context entry as modified so EF tracks changes.</summary>
    void Update(Group group);

    /// <summary>Commits all pending changes to the database.</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
