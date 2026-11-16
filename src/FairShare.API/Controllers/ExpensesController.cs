using FairShare.Application.DTOs.Expenses;
using FairShare.Application.UseCases.Expenses;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace FairShare.API.Controllers;

/// <summary>Manages expenses for guest groups.</summary>
[ApiController]
[Route("api/groups/{guestToken:guid}/expenses")]
[Produces("application/json")]
public class ExpensesController : ControllerBase
{
    private readonly CreateExpenseUseCase _createExpense;
    private readonly UpdateExpenseUseCase _updateExpense;
    private readonly DeleteExpenseUseCase _deleteExpense;
    private readonly GetSummaryUseCase _getSummary;
    private readonly IValidator<CreateExpenseRequest> _createValidator;
    private readonly IValidator<UpdateExpenseRequest> _updateValidator;

    public ExpensesController(
        CreateExpenseUseCase createExpense,
        UpdateExpenseUseCase updateExpense,
        DeleteExpenseUseCase deleteExpense,
        GetSummaryUseCase getSummary,
        IValidator<CreateExpenseRequest> createValidator,
        IValidator<UpdateExpenseRequest> updateValidator)
    {
        _createExpense   = createExpense;
        _updateExpense   = updateExpense;
        _deleteExpense   = deleteExpense;
        _getSummary      = getSummary;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>Adds an expense to the group.</summary>
    /// <response code="201">Expense created.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="404">Group not found.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ExpenseResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        [FromRoute] Guid guestToken,
        [FromBody] CreateExpenseRequest request,
        CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var result = await _createExpense.ExecuteAsync(guestToken, request, ct);

        return result.Status switch
        {
            ExpenseResultStatus.Success               => StatusCode(StatusCodes.Status201Created, result.Expense),
            ExpenseResultStatus.NotFound              => NotFound(NotFoundProblem("Group not found.")),
            ExpenseResultStatus.PayerNotInGroup       => PayerNotInGroupProblem(),
            ExpenseResultStatus.ParticipantNotInGroup => ParticipantNotInGroupProblem(),
            _ => StatusCode(500)
        };
    }

    /// <summary>Updates an existing expense and recalculates splits.</summary>
    /// <response code="200">Expense updated.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="404">Group or expense not found.</response>
    [HttpPut("{expenseId:guid}")]
    [ProducesResponseType(typeof(ExpenseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid guestToken,
        [FromRoute] Guid expenseId,
        [FromBody] UpdateExpenseRequest request,
        CancellationToken ct)
    {
        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));

        var result = await _updateExpense.ExecuteAsync(guestToken, expenseId, request, ct);

        return result.Status switch
        {
            ExpenseResultStatus.Success               => Ok(result.Expense),
            ExpenseResultStatus.NotFound              => NotFound(NotFoundProblem("Group or expense not found.")),
            ExpenseResultStatus.PayerNotInGroup       => PayerNotInGroupProblem(),
            ExpenseResultStatus.ParticipantNotInGroup => ParticipantNotInGroupProblem(),
            ExpenseResultStatus.PayerSumMismatch      => PayerSumMismatchProblem(),
            _ => StatusCode(500)
        };
    }

    /// <summary>Permanently deletes an expense along with its payers and splits.</summary>
    /// <response code="204">Expense deleted.</response>
    /// <response code="404">Group or expense not found.</response>
    [HttpDelete("{expenseId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid guestToken,
        [FromRoute] Guid expenseId,
        CancellationToken ct)
    {
        var result = await _deleteExpense.ExecuteAsync(guestToken, expenseId, ct);

        return result switch
        {
            DeleteExpenseResult.Success          => NoContent(),
            DeleteExpenseResult.GroupNotFound    => NotFound(NotFoundProblem("Group not found.")),
            DeleteExpenseResult.ExpenseNotFound  => NotFound(NotFoundProblem("Expense not found.")),
            _ => StatusCode(500)
        };
    }

    private IActionResult PayerNotInGroupProblem() =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["Payers"] = ["Every payer must be a member of this group."]
        }));

    private IActionResult PayerSumMismatchProblem() =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["Payers"] = ["Sum of payer amounts must equal the total amount."]
        }));

    private IActionResult ParticipantNotInGroupProblem() =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["ParticipantMemberIds"] = ["Every participant must be a member of this group."]
        }));

    private static ProblemDetails NotFoundProblem(string detail) => new()
    {
        Title  = "Not Found",
        Detail = detail,
        Status = StatusCodes.Status404NotFound
    };
}
