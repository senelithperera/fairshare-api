namespace FairShare.Application.DTOs.Expenses;

/// <summary>Full expense detail returned after create/update.</summary>
public record ExpenseResponse(
    string ExpenseId,
    string GroupId,
    string Title,
    decimal TotalAmount,
    string CreatedAt,
    IReadOnlyList<ExpensePayerResponse> Payers,
    IReadOnlyList<ExpenseSplitResponse> Splits);

/// <summary>Payer detail inside an expense response.</summary>
public record ExpensePayerResponse(string MemberId, string DisplayName, decimal AmountPaid);

/// <summary>Split detail inside an expense response.</summary>
public record ExpenseSplitResponse(string MemberId, string DisplayName, decimal ShareAmount);
