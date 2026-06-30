using FairShare.Application.DTOs.Expenses;
using FairShare.Application.UseCases.Expenses;
using Microsoft.AspNetCore.Mvc;

namespace FairShare.API.Controllers;

/// <summary>Returns the group financial summary including balances and settlements.</summary>
[ApiController]
[Route("api/groups/{guestToken:guid}/summary")]
[Produces("application/json")]
public class SummaryController : ControllerBase
{
    private readonly GetSummaryUseCase _getSummary;

    public SummaryController(GetSummaryUseCase getSummary) => _getSummary = getSummary;

    /// <summary>
    /// Calculates and returns total spent, per-member balances, and the minimal settlement list.
    /// </summary>
    /// <response code="200">Summary calculated.</response>
    /// <response code="404">Group not found.</response>
    [HttpGet]
    [ProducesResponseType(typeof(SummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get([FromRoute] Guid guestToken, CancellationToken ct)
    {
        var result = await _getSummary.ExecuteAsync(guestToken, ct);
        if (result is null)
            return NotFound(new ProblemDetails
            {
                Title  = "Not Found",
                Detail = "Group not found.",
                Status = StatusCodes.Status404NotFound
            });

        return Ok(result);
    }
}
