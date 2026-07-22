namespace FairShare.Application.DTOs.Groups;

/// <summary>Minimal group info returned after creation.</summary>
public record CreateGroupResponse(
    string GroupId,
    string GuestToken,
    string Name,
    string Currency);

/// <summary>Full group detail returned by GET /api/groups/{guestToken}.</summary>
public record GroupDetailResponse(
    string GroupId,
    string GuestToken,
    string Name,
    string Currency,
    string CreatedAt,
    bool IsArchived,
    IReadOnlyList<GroupMemberResponse> Members,
    IReadOnlyList<ExpenseSummaryResponse> Expenses);

/// <summary>Member data nested inside a group response.</summary>
public record GroupMemberResponse(
    string MemberId,
    string DisplayName,
    decimal? BudgetCap,
    decimal TotalAmountPaid,
    string JoinedAt);

/// <summary>Expense summary nested inside a group response.</summary>
public record ExpenseSummaryResponse(
    string ExpenseId,
    string Title,
    decimal TotalAmount,
    string CreatedAt,
    IReadOnlyList<PayerResponse> Payers,
    IReadOnlyList<SplitResponse> Splits);

/// <summary>Payer entry nested inside an expense summary.</summary>
public record PayerResponse(string MemberId, string DisplayName, decimal AmountPaid);

/// <summary>Split entry nested inside an expense summary — who participates and their share.</summary>
public record SplitResponse(string MemberId, string DisplayName, decimal ShareAmount);
