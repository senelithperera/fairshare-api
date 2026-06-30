using FairShare.Domain.Entities;

namespace FairShare.Application.Interfaces;

/// <summary>Persistence contract for <see cref="User"/> records.</summary>
public interface IUserRepository
{
    /// <summary>Retrieves a user by primary key.</summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Retrieves a user by email address (case-insensitive).</summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>Returns true when the email is already registered.</summary>
    Task<bool> ExistsAsync(string email, CancellationToken ct = default);

    /// <summary>Finds a user by their stored (hashed) refresh token. Used during token rotation.</summary>
    Task<User?> GetByRefreshTokenHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Persists a new user.</summary>
    Task AddAsync(User user, CancellationToken ct = default);

    /// <summary>Marks the user as modified.</summary>
    void Update(User user);

    /// <summary>Commits all pending changes to the database.</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
