using FairShare.Application.DTOs.Groups;
using FairShare.Application.Interfaces;
using FairShare.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace FairShare.Application.UseCases.Groups;

/// <summary>Creates a new group (guest or authenticated) and returns the creation response.</summary>
public class CreateGroupUseCase
{
    private readonly IGroupRepository _groups;
    private readonly ILogger<CreateGroupUseCase> _logger;

    public CreateGroupUseCase(IGroupRepository groups, ILogger<CreateGroupUseCase> logger)
    {
        _groups = groups;
        _logger = logger;
    }

    /// <summary>Executes the use case.</summary>
    /// <param name="request">Group creation details.</param>
    /// <param name="ownerUserId">Set when the group is created by an authenticated user; null for guest groups.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<CreateGroupResponse> ExecuteAsync(
        CreateGroupRequest request,
        Guid? ownerUserId,
        CancellationToken ct = default)
    {
        var group = new Group
        {
            Id              = Guid.NewGuid(),
            Name            = request.Name.Trim(),
            GuestToken      = Guid.NewGuid(),
            Currency        = request.Currency.ToUpperInvariant(),
            CreatedByUserId = ownerUserId,
            CreatedAt       = DateTime.UtcNow,
            IsArchived      = false
        };

        await _groups.AddAsync(group, ct);
        await _groups.SaveChangesAsync(ct);

        _logger.LogInformation("Created group {GroupId} (guestToken={GuestToken})", group.Id, group.GuestToken);

        return new CreateGroupResponse(
            group.Id.ToString(),
            group.GuestToken.ToString(),
            group.Name,
            group.Currency);
    }
}
