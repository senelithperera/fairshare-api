using FairShare.Domain.Services;
using FluentAssertions;

namespace FairShare.Tests.Domain;

/// <summary>
/// Settlements must use the fewest possible transfers. Plain greedy matching (largest creditor
/// with largest debtor) is capped at n − 1 transfers but misses groups of people whose balances
/// already cancel out, so it can make more transfers than necessary.
/// </summary>
public class MinimalSettlementTests
{
    private static MemberInput Member(string name, decimal balance)
        => new(Guid.NewGuid(), name, Math.Max(balance, 0m), Math.Max(-balance, 0m));

    /// <summary>Applies every transfer and asserts that everyone ends at exactly zero.</summary>
    private static void ShouldSettleEveryone(IReadOnlyList<MemberInput> members, IReadOnlyList<Settlement> settlements)
    {
        var remaining = members.ToDictionary(m => m.MemberId, m => m.TotalAmountPaid - m.FairShare);
        foreach (var s in settlements)
        {
            s.Amount.Should().BePositive();
            remaining[s.FromMemberId] += s.Amount;
            remaining[s.ToMemberId]   -= s.Amount;
        }
        remaining.Values.Should().OnlyContain(b => b == 0m);
    }

    [Fact]
    public void TwoIndependentGroups_AreSettledSeparately_InThreeTransfers()
    {
        // Dinner: Alice paid 9,000 for Alice, Dan and Eve (3,000 each).
        // Taxi:   Bob paid 10,000 for Bob and Carol (5,000 each).
        // Greedy pairs Carol with Alice first and needs 4 transfers; the minimum is 3.
        var alice = Member("Alice",  6000m);
        var bob   = Member("Bob",    5000m);
        var carol = Member("Carol", -5000m);
        var dan   = Member("Dan",   -3000m);
        var eve   = Member("Eve",   -3000m);
        var members = new[] { alice, bob, carol, dan, eve };

        var result = SplitCalculator.Calculate(members);

        result.Settlements.Should().HaveCount(3);
        result.Settlements.Should().ContainSingle(s => s.FromMemberId == carol.MemberId && s.ToMemberId == bob.MemberId   && s.Amount == 5000m);
        result.Settlements.Should().ContainSingle(s => s.FromMemberId == dan.MemberId   && s.ToMemberId == alice.MemberId && s.Amount == 3000m);
        result.Settlements.Should().ContainSingle(s => s.FromMemberId == eve.MemberId   && s.ToMemberId == alice.MemberId && s.Amount == 3000m);
        ShouldSettleEveryone(members, result.Settlements);
    }

    [Fact]
    public void ExpenseSettlements_UseTheSameMinimalMatching()
    {
        var a = new ExpenseMemberInput(Guid.NewGuid(), "A", 60m, 0m);   // +60
        var b = new ExpenseMemberInput(Guid.NewGuid(), "B", 50m, 0m);   // +50
        var c = new ExpenseMemberInput(Guid.NewGuid(), "C", 0m, 50m);   // −50
        var d = new ExpenseMemberInput(Guid.NewGuid(), "D", 0m, 30m);   // −30
        var e = new ExpenseMemberInput(Guid.NewGuid(), "E", 0m, 30m);   // −30

        SplitCalculator.CalculateExpenseSettlements([a, b, c, d, e]).Should().HaveCount(3);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void RandomGroups_NeverNeedMoreThan_MembersMinusZeroSumGroups(int seed)
    {
        // Build balances out of k groups that each net to zero, then shuffle them together.
        // n − k transfers are always enough, so the result must not exceed that.
        var random  = new Random(seed);
        var members = new List<MemberInput>();
        int groups  = 0;
        while (true)
        {
            int size = random.Next(2, 5);
            if (members.Count + size > SplitCalculator.MaxExactSettlementMembers) break;

            var cents = Enumerable.Range(0, size - 1).Select(_ => (long)random.Next(1, 50_000) * (random.Next(2) == 0 ? 1 : -1)).ToList();
            long last = -cents.Sum();
            if (last == 0) continue;
            cents.Add(last);

            members.AddRange(cents.Select(c => Member($"M{members.Count}", c / 100m)));
            groups++;
        }
        members = members.OrderBy(_ => random.Next()).ToList();

        var result = SplitCalculator.Calculate(members);

        result.Settlements.Count.Should().BeLessThanOrEqualTo(members.Count - groups);
        ShouldSettleEveryone(members, result.Settlements);
    }

    [Fact]
    public void LargeGroup_FallsBackToGreedy_AndStillSettlesEveryone()
    {
        // Past the exact-search limit: one payer covering everyone else.
        int count   = SplitCalculator.MaxExactSettlementMembers + 4;
        var members = new List<MemberInput> { Member("Payer", (count - 1) * 25m) };
        members.AddRange(Enumerable.Range(1, count - 1).Select(i => Member($"M{i}", -25m)));

        var result = SplitCalculator.Calculate(members);

        result.Settlements.Should().HaveCount(count - 1);
        ShouldSettleEveryone(members, result.Settlements);
    }
}
