using FairShare.Application.DTOs.Groups;
using FairShare.Application.UseCases.Groups;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FairShare.API.Extensions;
using Microsoft.AspNetCore.RateLimiting;

namespace FairShare.API.Controllers;

/// <summary>Manages group creation and retrieval for both guest and authenticated flows.</summary>
[ApiController]
[Route("api/groups")]
[Produces("application/json")]
public class GroupsController : ControllerBase
{
    private readonly CreateGroupUseCase _createGroup;
    private readonly GetGroupUseCase _getGroup;
    private readonly DeleteGroupUseCase _deleteGroup;
    private readonly IValidator<CreateGroupRequest> _createValidator;

    public GroupsController(
        CreateGroupUseCase createGroup,
        GetGroupUseCase getGroup,
        DeleteGroupUseCase deleteGroup,
        IValidator<CreateGroupRequest> createValidator)
    {
        _createGroup      = createGroup;
        _getGroup         = getGroup;
        _deleteGroup      = deleteGroup;
        _createValidator  = createValidator;
    }

    /// <summary>
    /// Creates a new group. When authenticated, the group is owned by the current user.
    /// Without auth, a guest group is created.
    /// </summary>
    /// <response code="201">Group created successfully.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="429">Too many groups created from this network in the last hour.</response>
    [HttpPost]
    [EnableRateLimiting(RateLimitingExtensions.GroupCreatePolicy)]
    [ProducesResponseType(typeof(CreateGroupResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateGroupRequest request,
        CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        Guid? ownerUserId = User.Identity?.IsAuthenticated == true
            ? Guid.Parse(User.FindFirst("sub")!.Value)
            : null;

        var response = await _createGroup.ExecuteAsync(request, ownerUserId, ct);
        return CreatedAtAction(nameof(GetByGuestToken), new { guestToken = response.GuestToken }, response);
    }

    /// <summary>Returns full group detail including members and active expenses.</summary>
    /// <response code="200">Group found.</response>
    /// <response code="404">Group not found.</response>
    [HttpGet("{guestToken:guid}")]
    [ProducesResponseType(typeof(GroupDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByGuestToken([FromRoute] Guid guestToken, CancellationToken ct)
    {
        var result = await _getGroup.ExecuteAsync(guestToken, ct);
        if (result is null)
            return NotFound(ProblemFor("Group not found.", 404));

        return Ok(result);
    }

    /// <summary>
    /// Permanently deletes the group with all its people and expenses. Anyone with the group's
    /// link can do this, and it can't be undone.
    /// </summary>
    /// <response code="204">Group deleted.</response>
    /// <response code="404">Group not found (or already deleted).</response>
    [HttpDelete("{guestToken:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] Guid guestToken, CancellationToken ct)
    {
        // The title is shown to people as-is: usually someone else deleted it first.
        if (!await _deleteGroup.ExecuteAsync(guestToken, ct))
            return NotFound(new ProblemDetails
            {
                Title  = "This group has already been deleted.",
                Detail = "Group not found.",
                Status = StatusCodes.Status404NotFound,
            });

        return NoContent();
    }

    private static ProblemDetails ProblemFor(string detail, int status) => new()
    {
        Title  = "Not Found",
        Detail = detail,
        Status = status
    };
}
