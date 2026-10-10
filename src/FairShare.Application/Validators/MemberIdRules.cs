namespace FairShare.Application.Validators;

/// <summary>Member-id checks shared by the create and update expense validators (payers and participants).</summary>
internal static class MemberIdRules
{
    public static bool BeAGuid(string memberId) => Guid.TryParse(memberId, out _);

    /// <summary>
    /// True when no member appears twice. Compares parsed GUIDs so that differently-formatted
    /// ids for the same member (e.g. upper vs lower case) still count as duplicates.
    /// Malformed ids are ignored here; the per-id rule reports them.
    /// </summary>
    public static bool BeDistinct(IEnumerable<string> memberIds)
    {
        var ids = memberIds
            .Select(id => Guid.TryParse(id, out var guid) ? guid : (Guid?)null)
            .OfType<Guid>()
            .ToList();
        return ids.Distinct().Count() == ids.Count;
    }
}
