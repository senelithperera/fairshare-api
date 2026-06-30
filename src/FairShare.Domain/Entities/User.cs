namespace FairShare.Domain.Entities;

/// <summary>Represents an authenticated application user.</summary>
public class User
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Unique email address used for login.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Bcrypt hash of the user's password.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Opaque refresh token (stored as hash in DB).</summary>
    public string? RefreshToken { get; set; }

    /// <summary>Expiry date of the current refresh token (UTC).</summary>
    public DateTime? RefreshTokenExpiry { get; set; }

    /// <summary>UTC timestamp of account creation.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Group memberships for this user.</summary>
    public ICollection<GroupMember> GroupMemberships { get; set; } = new List<GroupMember>();

    /// <summary>Groups owned by this user.</summary>
    public ICollection<Group> OwnedGroups { get; set; } = new List<Group>();
}
