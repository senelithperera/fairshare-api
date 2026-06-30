using FairShare.Application.DTOs;
using FairShare.Application.DTOs.Groups;
using FairShare.Application.Interfaces;

namespace FairShare.Application.UseCases.Users;

/// <summary>Returns all groups the authenticated user belongs to.</summary>
public class GetUserGroupsUseCase
{
    private readonly IGroupRepository _groups;

    public GetUserGroupsUseCase(IGroupRepository groups) => _groups = groups;

    /// <summary>Executes the use case.</summary>
    public async Task<PagedResponse<CreateGroupResponse>> ExecuteAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var groups = await _groups.GetAllForUserAsync(userId, ct);

        var items = groups.Select(g => new CreateGroupResponse(
            g.Id.ToString(),
            g.GuestToken.ToString(),
            g.Name,
            g.Currency)).ToList();

        return PagedResponse<CreateGroupResponse>.From(items);
    }
}
