namespace FairShare.Application.Interfaces;

/// <summary>Contract for password hashing and verification.</summary>
public interface IPasswordHasher
{
    /// <summary>Hashes a plain-text password using a one-way algorithm (e.g. BCrypt).</summary>
    string Hash(string plainPassword);

    /// <summary>Verifies a plain-text password against a stored hash.</summary>
    bool Verify(string plainPassword, string hash);
}
