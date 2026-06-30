using FairShare.Application.Interfaces;
using FairShare.Domain.Entities;
using FairShare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FairShare.Infrastructure.Repositories;

/// <summary>EF Core implementation of <see cref="IUserRepository"/>.</summary>
public class UserRepository : IUserRepository
{
    private readonly FairShareDbContext _db;

    public UserRepository(FairShareDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Users.FindAsync([id], ct);

    /// <inheritdoc/>
    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await _db.Users.FirstOrDefaultAsync(u => u.Email == email.ToLower(), ct);

    /// <inheritdoc/>
    public async Task<bool> ExistsAsync(string email, CancellationToken ct = default)
        => await _db.Users.AnyAsync(u => u.Email == email.ToLower(), ct);

    /// <inheritdoc/>
    public async Task<User?> GetByRefreshTokenHashAsync(string tokenHash, CancellationToken ct = default)
        => await _db.Users.FirstOrDefaultAsync(u => u.RefreshToken == tokenHash, ct);

    /// <inheritdoc/>
    public async Task AddAsync(User user, CancellationToken ct = default)
        => await _db.Users.AddAsync(user, ct);

    /// <inheritdoc/>
    public void Update(User user) => _db.Users.Update(user);

    /// <inheritdoc/>
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
