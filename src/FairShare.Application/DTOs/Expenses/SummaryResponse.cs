namespace FairShare.Application.DTOs.Expenses;

/// <summary>Returned by GET /api/groups/{guestToken}/summary.</summary>
public record SummaryResponse(
    decimal TotalSpent,
    IReadOnlyList<MemberBalanceResponse> MemberBalances,
    IReadOnlyList<SettlementResponse> Settlements);

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
