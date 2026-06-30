namespace FairShare.Domain.Entities;

/// <summary>
/// Stores the calculated share owed by a member for a specific expense.
/// Computed at write time and persisted for audit purposes.
/// </summary>
public class ExpenseSplit
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>FK to the parent expense.</summary>
    public Guid ExpenseId { get; set; }

    /// <summary>FK to the group member who owes this share.</summary>
    public Guid MemberId { get; set; }

    /// <summary>Calculated share amount in the group's currency.</summary>
    public decimal ShareAmount { get; set; }

    // Navigation properties
    public Expense Expense { get; set; } = null!;
    public GroupMember Member { get; set; } = null!;
}
