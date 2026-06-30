namespace FairShare.Application.DTOs.Expenses;

/// <summary>Request body for updating an existing expense (all fields optional).</summary>
public record UpdateExpenseRequest(
    string? Title,
    decimal? TotalAmount,
    IReadOnlyList<PayerRequest>? Payers);
