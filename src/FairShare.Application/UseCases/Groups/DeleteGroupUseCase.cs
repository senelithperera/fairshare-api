using FairShare.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace FairShare.Application.UseCases.Groups;

/// <summary>
/// Permanently deletes a group with all its people and expenses. Like every other change in
/// Phase 1, anyone holding the group's link may do it; there is no undo.
/// </summary>
public class DeleteGroupUseCase
{
    private readonly IGroupRepository _groups;
    private readonly ILogger<DeleteGroupUseCase> _logger;

    public DeleteGroupUseCase(IGroupRepository groups, ILogger<DeleteGroupUseCase> logger)
    {
        _groups = groups;
        _logger = logger;
    }

    /// <summary>Returns <c>false</c> when there is no group with that token.</summary>
    public async Task<bool> ExecuteAsync(Guid guestToken, CancellationToken ct = default)
    {
        var deleted = await _groups.DeleteByGuestTokenAsync(guestToken, ct);
        if (deleted)
            _logger.LogInformation("Group deleted permanently");
        return deleted;
    }
}
