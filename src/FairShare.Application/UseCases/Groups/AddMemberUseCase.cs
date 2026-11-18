using FairShare.Application.DTOs.Groups;
using FairShare.Application.Interfaces;
using FairShare.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace FairShare.Application.UseCases.Groups;

/// <summary>Outcome of adding a member.</summary>
public enum AddMemberStatus { Success, GroupNotFound, GroupFull }

/// <summary>Outcome of adding a member; <see cref="Member"/> is set on success.</summary>
public record AddMemberResult(AddMemberStatus Status, GroupMemberResponse? Member = null);

/// <summary>Adds a guest member to an existing group.</summary>
public class AddMemberUseCase
{
    private readonly IGroupRepository _groups;
    private readonly IGroupMemberRepository _members;
    private readonly ILogger<AddMemberUseCase> _logger;
    private readonly GroupLimits _limits;

    public AddMemberUseCase(
        IGroupRepository groups,
        IGroupMemberRepository members,
        ILogger<AddMemberUseCase> logger,
        GroupLimits? limits = null)
    {
        _groups  = groups;
        _members = members;
        _logger  = logger;
        _limits  = limits ?? GroupLimits.Default;
    }

    /// <summary>The limits in force (the API shows the member limit in its error message).</summary>
    public GroupLimits Limits => _limits;

    /// <summary>Executes the use case.</summary>
    /// <returns>The new member, or why it couldn't be added.</returns>
    public async Task<AddMemberResult> ExecuteAsync(
        Guid guestToken,
        AddMemberRequest request,
        CancellationToken ct = default)
    {
        var group = await _groups.GetByGuestTokenAsync(guestToken, ct);
        if (group is null) return new(AddMemberStatus.GroupNotFound);

        if (await _members.CountByGroupIdAsync(group.Id, ct) >= _limits.MaxMembersPerGroup)
            return new(AddMemberStatus.GroupFull);

        var member = new GroupMember
        {
            Id        = Guid.NewGuid(),
            GroupId   = group.Id,
            GuestName = request.GuestName.Trim(),
            JoinedAt  = DateTime.UtcNow
        };

        await _members.AddAsync(member, ct);
        await _members.SaveChangesAsync(ct);

        _logger.LogInformation("Added member {MemberId} to group {GroupId}", member.Id, group.Id);

        return new(AddMemberStatus.Success, new GroupMemberResponse(
            member.Id.ToString(),
            member.GuestName,
            0m,
            member.JoinedAt.ToString("o")));
    }
}
