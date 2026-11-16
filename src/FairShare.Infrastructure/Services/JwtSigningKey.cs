namespace FairShare.Infrastructure.Services;

/// <summary>Guards against running with a missing, weak or placeholder JWT signing key.</summary>
public static class JwtSigningKey
{
    /// <summary>HMAC-SHA256 needs at least 256 bits; 32 ASCII characters is the floor.</summary>
    public const int MinimumLength = 32;

    /// <summary>
    /// Returns the key if it is usable; otherwise throws, so a deployment that forgot to set
    /// <c>Jwt__Key</c> fails at startup instead of signing tokens with a value that is public
    /// in the repository.
    /// </summary>
    public static string Require(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) ||
            key.Length < MinimumLength ||
            key.Contains("REPLACE_WITH", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Jwt:Key must be a random secret of at least {MinimumLength} characters. " +
                "Set it with the Jwt__Key environment variable.");
        }

        return key;
    }
}
