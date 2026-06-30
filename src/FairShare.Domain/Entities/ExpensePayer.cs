namespace FairShare.Domain.Entities;

/// <summary>Records how much a specific member paid toward an expense.</summary>
public class ExpensePayer
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>FK to the parent expense.</summary>
    public Guid ExpenseId { get; set; }

    /// <summary>FK to the group member who paid.</summary>
    public Guid MemberId { get; set; }

    /// <summary>Amount this member contributed toward the expense total.</summary>
    public decimal AmountPaid { get; set; }

    // Navigation properties
    public Expense Expense { get; set; } = null!;
    public GroupMember Member { get; set; } = null!;
}
