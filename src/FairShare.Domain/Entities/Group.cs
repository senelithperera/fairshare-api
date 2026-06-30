namespace FairShare.Domain.Entities;

/// <summary>Represents an expense-sharing group.</summary>
public class Group
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Human-readable group name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Unique token used for anonymous/guest access.
    /// Present on all groups — even auth-owned ones — for easy sharing.
    /// </summary>
    public Guid GuestToken { get; set; }

    /// <summary>Nullable FK to the owning <see cref="User"/>. Null for guest-created groups.</summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>ISO 4217 currency code for expense amounts (default: "LKR").</summary>
    public string Currency { get; set; } = "LKR";

    /// <summary>UTC timestamp of group creation.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Soft-delete flag. Archived groups are excluded from list queries.</summary>
    public bool IsArchived { get; set; }

    // Navigation properties
    public User? CreatedByUser { get; set; }
    public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}
