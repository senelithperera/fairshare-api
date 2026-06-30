using FairShare.Application.DTOs;
using FairShare.Application.DTOs.Groups;
using FairShare.Application.DTOs.Users;
using FairShare.Application.UseCases.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FairShare.API.Controllers;

/// <summary>Manages the current authenticated user's profile and group memberships.</summary>
[ApiController]
[Authorize]
[Route("api/users")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly GetCurrentUserUseCase _getUser;
    private readonly UpdateCurrentUserUseCase _updateUser;
    private readonly GetUserGroupsUseCase _getUserGroups;

    public UsersController(
        GetCurrentUserUseCase getUser,
        UpdateCurrentUserUseCase updateUser,
        GetUserGroupsUseCase getUserGroups)
    {
        _getUser       = getUser;
        _updateUser    = updateUser;
        _getUserGroups = getUserGroups;
    }

    /// <summary>Returns the current authenticated user's profile.</summary>
    /// <response code="200">Profile returned.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _getUser.ExecuteAsync(userId, ct);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    /// <summary>Updates the current user's name or email.</summary>
    /// <response code="200">Profile updated.</response>
    /// <response code="409">New email is already in use.</response>
    [HttpPatch("me")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var (result, response) = await _updateUser.ExecuteAsync(userId, request, ct);

        return result switch
        {
            UpdateUserResult.NotFound   => NotFound(),
            UpdateUserResult.EmailTaken => Conflict(new ProblemDetails
            {
                Title  = "Conflict",
                Detail = "This email address is already used by another account.",
                Status = StatusCodes.Status409Conflict
            }),
            _ => Ok(response)
        };
    }

    /// <summary>Returns all non-archived groups the current user belongs to or owns.</summary>
    /// <response code="200">Groups returned.</response>
    [HttpGet("me/groups")]
    [ProducesResponseType(typeof(PagedResponse<CreateGroupResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MyGroups(CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var result = await _getUserGroups.ExecuteAsync(userId, ct);
        return Ok(result);
    }
}
