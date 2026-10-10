namespace FairShare.Domain.Entities;

/// <summary>Represents a group expense entry.</summary>
public class Expense
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>FK to the owning group.</summary>
    public Guid GroupId { get; set; }

    /// <summary>Short description of what was spent.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Full cost of the expense in the group's currency.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>FK to the <see cref="GroupMember"/> who recorded this expense. Optional.</summary>
    public Guid? CreatedByMemberId { get; set; }

    /// <summary>UTC timestamp of expense creation.</summary>
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Group Group { get; set; } = null!;
    public GroupMember? CreatedByMember { get; set; }
    public ICollection<ExpensePayer> Payers { get; set; } = new List<ExpensePayer>();
    public ICollection<ExpenseSplit> Splits { get; set; } = new List<ExpenseSplit>();
}
