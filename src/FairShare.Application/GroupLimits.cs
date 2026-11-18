namespace FairShare.Application;

/// <summary>
/// How big one group may get. Groups are open to anyone holding the share link, so these keep
/// a leaked link (or a script) from growing a group without bound, and keep the summary cheap
/// to compute. Sized for a circle of about a dozen friends, with plenty of room: real groups
/// are well under these. Configured under <c>Limits</c> (see README, Security).
/// </summary>
public sealed record GroupLimits
{
    /// <summary>
    /// Hard ceiling on people in a group, and so on payers or participants in one expense.
    /// Request validation uses it directly; <see cref="MaxMembersPerGroup"/> can't exceed it.
    /// </summary>
    public const int PeopleCeiling = 100;

    /// <summary>Defaults: 30 people and 1,000 expenses per group.</summary>
    public static readonly GroupLimits Default = new();

    /// <summary>Most people one group can have.</summary>
    public int MaxMembersPerGroup { get; init; } = 30;

    /// <summary>Most expenses one group can have.</summary>
    public int MaxExpensesPerGroup { get; init; } = 1000;

    /// <summary>Builds limits from configured values, kept within sensible bounds.</summary>
    public static GroupLimits From(int? maxMembers, int? maxExpenses) => new()
    {
        MaxMembersPerGroup  = Math.Clamp(maxMembers ?? Default.MaxMembersPerGroup, 1, PeopleCeiling),
        MaxExpensesPerGroup = Math.Max(1, maxExpenses ?? Default.MaxExpensesPerGroup),
    };
}
