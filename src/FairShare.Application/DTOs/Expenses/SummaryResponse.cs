namespace FairShare.Application.DTOs.Expenses;

/// <summary>Returned by GET /api/groups/{guestToken}/summary.</summary>
public record SummaryResponse(
    decimal TotalSpent,
    IReadOnlyList<MemberBalanceResponse> MemberBalances,
    IReadOnlyList<SettlementResponse> Settlements,
    IReadOnlyList<ExpenseSettlementGroupResponse> DetailedSettlements);

/// <summary>Balance entry for one member inside the summary response.</summary>
public record MemberBalanceResponse(
    string MemberId,
    string DisplayName,
    decimal Balance,
    bool IsCreditor);

/// <summary>A single cash transfer needed to settle the group.</summary>
public record SettlementResponse(
    string FromMemberId,
    string From,
    string ToMemberId,
    string To,
    decimal Amount);

/// <summary>
/// Settlements traceable back to a single expense — every pairing here reflects two people
/// who actually shared this specific expense, unlike the group-wide <see cref="SummaryResponse.Settlements"/>
/// which nets balances across the whole group and can pair up people who never shared an expense.
/// </summary>
public record ExpenseSettlementGroupResponse(
    string ExpenseId,
    string ExpenseTitle,
    IReadOnlyList<SettlementResponse> Settlements);
