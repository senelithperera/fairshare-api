namespace FairShare.Domain.Entities;

/// <summary>
/// Represents a participant inside a group.
/// Either a registered <see cref="User"/> (UserId set) or an anonymous guest (GuestName set).
/// </summary>
public class GroupMember
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>FK to the parent group.</summary>
    public Guid GroupId { get; set; }

    /// <summary>Optional FK to a registered user. Null for guest members.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Display name for guest members. Required when <see cref="UserId"/> is null.</summary>
    public string? GuestName { get; set; }

    /// <summary>UTC timestamp when the member joined the group.</summary>
    public DateTime JoinedAt { get; set; }

    // Navigation properties
    public Group Group { get; set; } = null!;
    public User? User { get; set; }
    public ICollection<ExpensePayer> PayerRecords { get; set; } = new List<ExpensePayer>();
    public ICollection<ExpenseSplit> SplitRecords { get; set; } = new List<ExpenseSplit>();
    public ICollection<Expense> CreatedExpenses { get; set; } = new List<Expense>();
}
