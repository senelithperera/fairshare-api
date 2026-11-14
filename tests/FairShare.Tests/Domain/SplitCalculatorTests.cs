using FairShare.Domain.Services;
using FluentAssertions;

namespace FairShare.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="SplitCalculator"/>.
/// Each test covers a distinct scenario; no infrastructure or EF dependencies.
///
/// <see cref="MemberInput.FairShare"/> stands in for "sum of this member's real
/// ExpenseSplit rows" — in production that's computed by GetSummaryUseCase from actual
/// persisted splits, which is exactly what makes per-expense participant exclusion
/// naturally flow through to these balances (an excluded member has no split row, so
/// their share for that expense is 0).
/// </summary>
public class SplitCalculatorTests
{
    // ─────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────

    private static MemberInput Member(string name, decimal paid, decimal fairShare)
        => new(Guid.NewGuid(), name, paid, fairShare);

    /// <summary>
    /// The same whole-cent equal split that CreateExpenseUseCase/UpdateExpenseUseCase persist
    /// as ExpenseSplit rows.
    /// </summary>
    private static IReadOnlyList<decimal> EqualShares(decimal total, int count)
        => SplitCalculator.SplitEvenly(total, count);

    // ─────────────────────────────────────────────────
    // Equal Split
    // ─────────────────────────────────────────────────

    [Fact]
    public void EqualSplit_ThreeMembers_BalancesAreCorrect()
    {
        // Alice paid 90, Bob paid 0, Carol paid 0. Total = 90, split equally between all three.
        // Each owes 30. Alice is creditor (+60), Bob debtor (-30), Carol debtor (-30).
        var alice = Member("Alice", 90m, 30m);
        var bob   = Member("Bob", 0m, 30m);
        var carol = Member("Carol", 0m, 30m);
        var members = new[] { alice, bob, carol };

        var result = SplitCalculator.Calculate(members);

        result.MemberBalances.Should().HaveCount(3);

        var aliceBalance = result.MemberBalances.Single(b => b.MemberId == alice.MemberId);
        var bobBalance   = result.MemberBalances.Single(b => b.MemberId == bob.MemberId);
        var carolBalance = result.MemberBalances.Single(b => b.MemberId == carol.MemberId);

        aliceBalance.Balance.Should().Be(60m);
        aliceBalance.IsCreditor.Should().BeTrue();

        bobBalance.Balance.Should().Be(-30m);
        bobBalance.IsCreditor.Should().BeFalse();

        carolBalance.Balance.Should().Be(-30m);
        carolBalance.IsCreditor.Should().BeFalse();
    }

    [Fact]
    public void EqualSplit_ThreeMembers_SettlementsAreMinimal()
    {
        var alice = Member("Alice", 90m, 30m);
        var bob   = Member("Bob", 0m, 30m);
        var carol = Member("Carol", 0m, 30m);

        var result = SplitCalculator.Calculate(new[] { alice, bob, carol });

        // Both debtors pay Alice directly — 2 settlements
        result.Settlements.Should().HaveCount(2);
        result.Settlements.All(s => s.ToMemberId == alice.MemberId).Should().BeTrue();
        result.Settlements.Sum(s => s.Amount).Should().Be(60m);
    }

    [Fact]
    public void EqualSplit_TwoMembers_BothPaidSome()
    {
        // Alice paid 70, Bob paid 30. Total = 100. Each owes 50.
        // Alice: +20 (creditor), Bob: -20 (debtor).
        var alice = Member("Alice", 70m, 50m);
        var bob   = Member("Bob", 30m, 50m);

        var result = SplitCalculator.Calculate(new[] { alice, bob });

        result.MemberBalances.Single(b => b.MemberId == alice.MemberId).Balance.Should().Be(20m);
        result.MemberBalances.Single(b => b.MemberId == bob.MemberId).Balance.Should().Be(-20m);

        result.Settlements.Should().HaveCount(1);
        result.Settlements[0].Amount.Should().Be(20m);
        result.Settlements[0].FromMemberId.Should().Be(bob.MemberId);
        result.Settlements[0].ToMemberId.Should().Be(alice.MemberId);
    }

    // ─────────────────────────────────────────────────
    // Participant Exclusion
    // ─────────────────────────────────────────────────

    [Fact]
    public void Exclusion_MemberNotInSplit_OwesNothingForThatExpense()
    {
        // A single $90 expense split only between Alice and Bob — Carol was excluded, so
        // she has no ExpenseSplit row and her fair share is 0.
        var alice = Member("Alice", 90m, 45m);
        var bob   = Member("Bob", 0m, 45m);
        var carol = Member("Carol", 0m,  0m);

        var result = SplitCalculator.Calculate(new[] { alice, bob, carol });

        result.MemberBalances.Single(b => b.MemberId == alice.MemberId).Balance.Should().Be(45m);
        result.MemberBalances.Single(b => b.MemberId == bob.MemberId).Balance.Should().Be(-45m);
        result.MemberBalances.Single(b => b.MemberId == carol.MemberId).Balance.Should().Be(0m);

        // Carol has nothing to do with this expense — no settlement should involve her.
        result.Settlements.Should().HaveCount(1);
        result.Settlements[0].FromMemberId.Should().Be(bob.MemberId);
        result.Settlements[0].ToMemberId.Should().Be(alice.MemberId);
        result.Settlements[0].Amount.Should().Be(45m);
    }

    [Fact]
    public void Exclusion_ExcludedMemberStillPaid_IsPureCreditor()
    {
        // Dave treats the others but isn't part of the split himself (e.g. he already ate).
        // $60 expense split between Bob and Carol only; Dave paid the full amount.
        var dave  = Member("Dave", 60m,  0m);
        var bob   = Member("Bob", 0m, 30m);
        var carol = Member("Carol", 0m, 30m);

        var result = SplitCalculator.Calculate(new[] { dave, bob, carol });

        result.MemberBalances.Single(b => b.MemberId == dave.MemberId).Balance.Should().Be(60m);
        result.MemberBalances.Single(b => b.MemberId == bob.MemberId).Balance.Should().Be(-30m);
        result.MemberBalances.Single(b => b.MemberId == carol.MemberId).Balance.Should().Be(-30m);
    }

    // ─────────────────────────────────────────────────
    // Rounding
    // ─────────────────────────────────────────────────

    [Theory]
    [InlineData("100",   3, "33.34,33.33,33.33")]
    [InlineData("2",     3, "0.67,0.67,0.66")]
    [InlineData("10",    3, "3.34,3.33,3.33")]
    [InlineData("1000",  7, "142.86,142.86,142.86,142.86,142.86,142.85,142.85")]
    [InlineData("15000", 9, "1666.67,1666.67,1666.67,1666.67,1666.67,1666.67,1666.66,1666.66,1666.66")]
    [InlineData("30000", 3, "10000,10000,10000")]
    [InlineData("0.01",  3, "0.01,0,0")]
    public void SplitEvenly_ProducesWholeCentSharesThatSumToTotal(string total, int count, string expected)
    {
        var totalAmount = decimal.Parse(total, System.Globalization.CultureInfo.InvariantCulture);
        var expectedShares = expected.Split(',')
            .Select(s => decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture));

        var shares = SplitCalculator.SplitEvenly(totalAmount, count);

        shares.Should().Equal(expectedShares);
        shares.Sum().Should().Be(totalAmount);
        // Every share must already be whole cents — otherwise the numeric(18,2) column
        // rounds each one independently on insert and the stored shares drift from the total.
        shares.Should().OnlyContain(s => s == Math.Round(s, 2));
    }

    [Fact]
    public void SplitEvenly_ZeroMembers_Throws()
    {
        var act = () => SplitCalculator.SplitEvenly(100m, 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void EqualSplit_Rounding_BalancesNetToExactlyZero()
    {
        // 15000 split nine ways — the real-data case where the old split stored
        // 1666.67 × 9 = 15000.03 and the last debtor's settlement was silently cut to 1666.64.
        var shares  = EqualShares(15000m, 9);
        var members = shares
            .Select((share, i) => Member($"M{i}", i == 0 ? 15000m : 0m, share))
            .ToList();

        var result = SplitCalculator.Calculate(members);

        result.MemberBalances.Sum(x => x.Balance).Should().Be(0m);

        // Every debtor's settlement matches their displayed balance exactly.
        foreach (var debtor in result.MemberBalances.Where(b => b.Balance < 0))
            result.Settlements.Single(s => s.FromMemberId == debtor.MemberId)
                .Amount.Should().Be(-debtor.Balance);
    }

    // ─────────────────────────────────────────────────
    // Edge Cases
    // ─────────────────────────────────────────────────

    [Fact]
    public void Calculate_NoMembers_ReturnsEmptyResult()
    {
        var result = SplitCalculator.Calculate([]);

        result.MemberBalances.Should().BeEmpty();
        result.Settlements.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_OneMember_NoSettlementsNeeded()
    {
        var solo = Member("Solo", 100m, 100m);

        var result = SplitCalculator.Calculate(new[] { solo });

        result.MemberBalances.Should().HaveCount(1);
        result.Settlements.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_ZeroTotalSpent_AllBalancesZero()
    {
        var a = Member("A", 0m, 0m);
        var b = Member("B", 0m, 0m);

        var result = SplitCalculator.Calculate(new[] { a, b });

        result.MemberBalances.Should().AllSatisfy(m => m.Balance.Should().Be(0m));
        result.Settlements.Should().BeEmpty();
    }

    [Fact]
    public void EqualSplit_AllMembersPaidEqual_NoSettlements()
    {
        // Each paid exactly their share → no debts
        var a = Member("A", 30m, 30m);
        var b = Member("B", 30m, 30m);
        var c = Member("C", 30m, 30m);

        var result = SplitCalculator.Calculate(new[] { a, b, c });

        result.MemberBalances.Should().AllSatisfy(m => m.Balance.Should().Be(0m));
        result.Settlements.Should().BeEmpty();
    }

    [Fact]
    public void SimplifyDebts_MultipleCreditors_TransactionsMinimized()
    {
        // Alice: +30, Bob: +10, Carol: -20, Dave: -20
        // Greedy: Carol pays Alice 20, Dave pays Alice 10, Dave pays Bob 10 → 3 transactions
        var alice = new MemberInput(Guid.NewGuid(), "Alice", 60m, 30m); // paid 60, share=30 → +30
        var bob   = new MemberInput(Guid.NewGuid(), "Bob", 40m, 30m); // paid 40, share=30 → +10
        var carol = new MemberInput(Guid.NewGuid(), "Carol", 10m, 30m); // paid 10, share=30 → -20
        var dave  = new MemberInput(Guid.NewGuid(), "Dave", 10m, 30m); // paid 10, share=30 → -20

        var result = SplitCalculator.Calculate(new[] { alice, bob, carol, dave });

        result.MemberBalances.Sum(b => b.Balance).Should().BeApproximately(0m, 0.001m);

        decimal totalSettled = result.Settlements.Sum(s => s.Amount);
        decimal totalOwed    = result.MemberBalances.Where(b => b.Balance > 0).Sum(b => b.Balance);
        totalSettled.Should().BeApproximately(totalOwed, 0.001m);
    }
}
