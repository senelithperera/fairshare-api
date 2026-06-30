using FairShare.Application.DTOs.Groups;
using FairShare.Application.UseCases.Groups;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FairShare.API.Controllers;

/// <summary>Manages group creation and retrieval for both guest and authenticated flows.</summary>
[ApiController]
[Route("api/groups")]
[Produces("application/json")]
public class GroupsController : ControllerBase
{
    private readonly CreateGroupUseCase _createGroup;
    private readonly GetGroupUseCase _getGroup;
    private readonly IValidator<CreateGroupRequest> _createValidator;

    public GroupsController(
        CreateGroupUseCase createGroup,
        GetGroupUseCase getGroup,
        IValidator<CreateGroupRequest> createValidator)
    {
        _createGroup      = createGroup;
        _getGroup         = getGroup;
        _createValidator  = createValidator;
    }

    /// <summary>
    /// Creates a new group. When authenticated, the group is owned by the current user.
    /// Without auth, a guest group is created.
    /// </summary>
    /// <response code="201">Group created successfully.</response>
    /// <response code="400">Validation error.</response>
    [HttpPost]
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

    private static ProblemDetails ProblemFor(string detail, int status) => new()
    {
        Title  = "Not Found",
        Detail = detail,
        Status = status
    };
}
