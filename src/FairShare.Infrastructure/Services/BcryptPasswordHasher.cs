using FairShare.Application.Interfaces;

namespace FairShare.Infrastructure.Services;

/// <summary>BCrypt-based implementation of <see cref="IPasswordHasher"/>.</summary>
public class BcryptPasswordHasher : IPasswordHasher
{
    /// <inheritdoc/>
    public string Hash(string plainPassword)
        => BCrypt.Net.BCrypt.HashPassword(plainPassword, workFactor: 12);

    /// <inheritdoc/>
    public bool Verify(string plainPassword, string hash)
        => BCrypt.Net.BCrypt.Verify(plainPassword, hash);
}
