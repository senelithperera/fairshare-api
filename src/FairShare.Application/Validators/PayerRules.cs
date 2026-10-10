using FairShare.Application.DTOs.Expenses;

namespace FairShare.Application.Validators;

/// <summary>Payer checks shared by the create and update expense validators.</summary>
internal static class PayerRules
{
    public static bool BeAGuid(string memberId) => Guid.TryParse(memberId, out _);

    /// <summary>
    /// True when no member appears twice. Compares parsed GUIDs so that differently-formatted
    /// ids for the same member (e.g. upper vs lower case) still count as duplicates.
    /// Malformed ids are ignored here; the per-payer rule reports them.
    /// </summary>
    public static bool HaveDistinctMembers(IReadOnlyList<PayerRequest> payers)
    {
        var ids = payers
            .Select(p => Guid.TryParse(p.MemberId, out var id) ? id : (Guid?)null)
            .OfType<Guid>()
            .ToList();
        return ids.Distinct().Count() == ids.Count;
    }
}
