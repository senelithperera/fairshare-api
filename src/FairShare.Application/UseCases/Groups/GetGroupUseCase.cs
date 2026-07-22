using FairShare.Application.DTOs.Groups;
using FairShare.Application.Interfaces;
using FairShare.Domain.Entities;

namespace FairShare.Application.UseCases.Groups;

/// <summary>Retrieves full group detail by guest token.</summary>
public class GetGroupUseCase
{
    private readonly IGroupRepository _groups;

    public GetGroupUseCase(IGroupRepository groups) => _groups = groups;

    /// <summary>Executes the use case.</summary>
    /// <param name="guestToken">The group's public guest token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Full group detail, or null when not found.</returns>
    public async Task<GroupDetailResponse?> ExecuteAsync(Guid guestToken, CancellationToken ct = default)
    {
        var group = await _groups.GetByGuestTokenAsync(guestToken, ct);
        if (group is null) return null;

        return MapToResponse(group);
    }

    private static GroupDetailResponse MapToResponse(Group g) => new(
        g.Id.ToString(),
        g.GuestToken.ToString(),
        g.Name,
        g.Currency,
        g.CreatedAt.ToString("o"),
        g.IsArchived,
        g.Members.Select(m => new GroupMemberResponse(
            m.Id.ToString(),
            m.GuestName ?? m.User?.Name ?? "Unknown",
            m.BudgetCap,
            m.PayerRecords.Sum(p => p.AmountPaid),
            m.JoinedAt.ToString("o"))).ToList(),
        g.Expenses
            .Where(e => !e.IsDeleted)
            .Select(e => new ExpenseSummaryResponse(
                e.Id.ToString(),
                e.Title,
                e.TotalAmount,
                e.CreatedAt.ToString("o"),
                e.Payers.Select(p => new PayerResponse(
                    p.MemberId.ToString(),
                    p.Member.GuestName ?? p.Member.User?.Name ?? "Unknown",
                    p.AmountPaid)).ToList(),
                e.Splits.Select(s => new SplitResponse(
                    s.MemberId.ToString(),
                    s.Member.GuestName ?? s.Member.User?.Name ?? "Unknown",
                    s.ShareAmount)).ToList())).ToList());
}
