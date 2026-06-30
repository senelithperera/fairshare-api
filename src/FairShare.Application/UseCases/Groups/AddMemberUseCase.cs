using FairShare.Application.DTOs.Groups;
using FairShare.Application.Interfaces;
using FairShare.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace FairShare.Application.UseCases.Groups;

/// <summary>Adds a guest member to an existing group.</summary>
public class AddMemberUseCase
{
    private readonly IGroupRepository _groups;
    private readonly IGroupMemberRepository _members;
    private readonly ILogger<AddMemberUseCase> _logger;

    public AddMemberUseCase(
        IGroupRepository groups,
        IGroupMemberRepository members,
        ILogger<AddMemberUseCase> logger)
    {
        _groups  = groups;
        _members = members;
        _logger  = logger;
    }

    /// <summary>Executes the use case.</summary>
    /// <returns>The newly created member response, or null when the group is not found.</returns>
    public async Task<GroupMemberResponse?> ExecuteAsync(
        Guid guestToken,
        AddMemberRequest request,
        CancellationToken ct = default)
    {
        var group = await _groups.GetByGuestTokenAsync(guestToken, ct);
        if (group is null) return null;

        var member = new GroupMember
        {
            Id        = Guid.NewGuid(),
            GroupId   = group.Id,
            GuestName = request.GuestName.Trim(),
            BudgetCap = request.BudgetCap,
            JoinedAt  = DateTime.UtcNow
        };

        await _members.AddAsync(member, ct);
        await _members.SaveChangesAsync(ct);

        _logger.LogInformation("Added member {MemberId} to group {GroupId}", member.Id, group.Id);

        return new GroupMemberResponse(
            member.Id.ToString(),
            member.GuestName,
            member.BudgetCap,
            0m,
            member.JoinedAt.ToString("o"));
    }
}
