namespace FairShare.Application.DTOs.Expenses;

/// <summary>Request body for adding an expense to a group.</summary>
public record CreateExpenseRequest(
    string Title,
    decimal TotalAmount,
    IReadOnlyList<PayerRequest> Payers);

/// <summary>One payer entry inside a create/update expense request.</summary>
public record PayerRequest(string MemberId, decimal AmountPaid);
