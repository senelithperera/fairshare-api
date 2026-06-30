using FairShare.Application.DTOs.Groups;
using FairShare.Application.UseCases.Groups;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace FairShare.API.Controllers;

/// <summary>Manages group membership for guest groups.</summary>
[ApiController]
[Route("api/groups/{guestToken:guid}/members")]
[Produces("application/json")]
public class MembersController : ControllerBase
{
    private readonly AddMemberUseCase _addMember;
    private readonly UpdateMemberUseCase _updateMember;
    private readonly DeleteMemberUseCase _deleteMember;
    private readonly IValidator<AddMemberRequest> _addValidator;
    private readonly IValidator<UpdateMemberRequest> _updateValidator;

    public MembersController(
        AddMemberUseCase addMember,
        UpdateMemberUseCase updateMember,
        DeleteMemberUseCase deleteMember,
        IValidator<AddMemberRequest> addValidator,
        IValidator<UpdateMemberRequest> updateValidator)
    {
        _addMember       = addMember;
        _updateMember    = updateMember;
        _deleteMember    = deleteMember;
        _addValidator    = addValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>Adds a guest member to a group.</summary>
    /// <response code="201">Member added.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="404">Group not found.</response>
    [HttpPost]
    [ProducesResponseType(typeof(GroupMemberResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Add(
        [FromRoute] Guid guestToken,
        [FromBody] AddMemberRequest request,
        CancellationToken ct)
    {
        var validation = await _addValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var result = await _addMember.ExecuteAsync(guestToken, request, ct);
        if (result is null)
            return NotFound(NotFoundProblem("Group not found."));

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Updates a guest member's name or budget cap.</summary>
    /// <response code="200">Member updated.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="404">Group or member not found.</response>
    [HttpPatch("{memberId:guid}")]
    [ProducesResponseType(typeof(GroupMemberResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid guestToken,
        [FromRoute] Guid memberId,
        [FromBody] UpdateMemberRequest request,
        CancellationToken ct)
    {
        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var result = await _updateMember.ExecuteAsync(guestToken, memberId, request, ct);
        if (result is null)
            return NotFound(NotFoundProblem("Group or member not found."));

        return Ok(result);
    }

    /// <summary>Removes a guest member (only when they have no associated expenses).</summary>
    /// <response code="204">Member removed.</response>
    /// <response code="404">Group or member not found.</response>
    /// <response code="409">Member has associated expenses and cannot be deleted.</response>
    [HttpDelete("{memberId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid guestToken,
        [FromRoute] Guid memberId,
        CancellationToken ct)
    {
        var result = await _deleteMember.ExecuteAsync(guestToken, memberId, ct);

        return result switch
        {
            DeleteMemberResult.Success          => NoContent(),
            DeleteMemberResult.GroupNotFound    => NotFound(NotFoundProblem("Group not found.")),
            DeleteMemberResult.MemberNotFound   => NotFound(NotFoundProblem("Member not found.")),
            DeleteMemberResult.MemberHasExpenses => Conflict(new ProblemDetails
            {
                Title  = "Conflict",
                Detail = "Member cannot be removed while they have associated expenses.",
                Status = StatusCodes.Status409Conflict
            }),
            _ => StatusCode(500)
        };
    }

    private static ProblemDetails NotFoundProblem(string detail) => new()
    {
        Title  = "Not Found",
        Detail = detail,
        Status = StatusCodes.Status404NotFound
    };
}
