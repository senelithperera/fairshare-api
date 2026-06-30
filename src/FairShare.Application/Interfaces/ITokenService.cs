using FairShare.Domain.Entities;

namespace FairShare.Application.Interfaces;

/// <summary>Contract for JWT access-token generation and refresh-token lifecycle management.</summary>
public interface ITokenService
{
    /// <summary>Generates a signed JWT access token for the given user.</summary>
    /// <param name="user">The authenticated user whose claims populate the token.</param>
    /// <returns>A signed JWT string.</returns>
    string GenerateAccessToken(User user);

    /// <summary>Generates a cryptographically random opaque refresh token string.</summary>
    /// <returns>Plain-text refresh token (must be hashed before storage).</returns>
    string GenerateRefreshToken();

    /// <summary>Returns the SHA-256 hash of a plain-text refresh token for safe storage.</summary>
    string HashRefreshToken(string plainToken);

    /// <summary>Validates a plain-text refresh token against its stored hash.</summary>
    bool ValidateRefreshToken(string plainToken, string storedHash);
}
