using FairShare.Domain.Services;
using FluentAssertions;

namespace FairShare.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="SplitCalculator"/>.
/// Each test covers a distinct scenario; no infrastructure or EF dependencies.
///
/// <see cref="MemberInput.RawFairShare"/> stands in for "sum of this member's real
/// ExpenseSplit rows" — in production that's computed by GetSummaryUseCase from actual
/// persisted splits, which is exactly what makes per-expense participant exclusion
/// naturally flow through to these balances (an excluded member has no split row, so
/// their raw share for that expense is 0).
/// </summary>
public class SplitCalculatorTests
{
    // ─────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────

    private static MemberInput Member(string name, decimal? cap, decimal paid, decimal rawFairShare)
        => new(Guid.NewGuid(), name, cap, paid, rawFairShare);

    /// <summary>
    /// Mirrors the production flat equal-split convention used by
    /// CreateExpenseUseCase/UpdateExpenseUseCase's BuildExpenseSplits: divide evenly,
    /// assign the rounding remainder to the first member.
    /// </summary>
    private static decimal[] EqualRawShares(decimal total, int count)
    {
        decimal equalShare = total / count;
        decimal remainder  = total - equalShare * count;
        var shares = new decimal[count];
        for (int i = 0; i < count; i++)
            shares[i] = i == 0 ? equalShare + remainder : equalShare;
        return shares;
    }

    // ─────────────────────────────────────────────────
    // Equal Split
    // ─────────────────────────────────────────────────

    [Fact]
    public void EqualSplit_ThreeMembers_BalancesAreCorrect()
    {
        // Alice paid 90, Bob paid 0, Carol paid 0. Total = 90, split equally between all three.
        // Each owes 30. Alice is creditor (+60), Bob debtor (-30), Carol debtor (-30).
        var alice = Member("Alice", null, 90m, 30m);
        var bob   = Member("Bob",   null,  0m, 30m);
        var carol = Member("Carol", null,  0m, 30m);
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
        var alice = Member("Alice", null, 90m, 30m);
        var bob   = Member("Bob",   null,  0m, 30m);
        var carol = Member("Carol", null,  0m, 30m);

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
        var alice = Member("Alice", null, 70m, 50m);
        var bob   = Member("Bob",   null, 30m, 50m);

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
        // she has no ExpenseSplit row and her raw fair share is 0.
        var alice = Member("Alice", null, 90m, 45m);
        var bob   = Member("Bob",   null,  0m, 45m);
        var carol = Member("Carol", null,  0m,  0m);

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
        var dave  = Member("Dave",  null, 60m,  0m);
        var bob   = Member("Bob",   null,  0m, 30m);
        var carol = Member("Carol", null,  0m, 30m);

        var result = SplitCalculator.Calculate(new[] { dave, bob, carol });

        result.MemberBalances.Single(b => b.MemberId == dave.MemberId).Balance.Should().Be(60m);
        result.MemberBalances.Single(b => b.MemberId == bob.MemberId).Balance.Should().Be(-30m);
        result.MemberBalances.Single(b => b.MemberId == carol.MemberId).Balance.Should().Be(-30m);
    }

    // ─────────────────────────────────────────────────
    // Capped Split
    // ─────────────────────────────────────────────────

    [Fact]
    public void CappedSplit_OneCappedOneUncapped_SharesAreCorrect()
    {
        // Total = 100 split equally between Dave and Eve → raw share 50 each. Dave paid the
        // whole bill. Dave capped at 20 → his raw (50) exceeds his cap, so he only owes 20;
        // the 30 excess spills onto the only uncapped member, Eve (50 + 30 = 80).
        var dave = Member("Dave", 20m, 100m, 50m);
        var eve  = Member("Eve",  null,   0m, 50m);

        var result = SplitCalculator.Calculate(new[] { dave, eve });

        result.MemberBalances.Single(b => b.MemberId == dave.MemberId).Balance.Should().Be(80m);
        result.MemberBalances.Single(b => b.MemberId == eve.MemberId).Balance.Should().Be(-80m);

        result.Settlements.Should().HaveCount(1);
        result.Settlements[0].Amount.Should().Be(80m);
        result.Settlements[0].FromMemberId.Should().Be(eve.MemberId);
        result.Settlements[0].ToMemberId.Should().Be(dave.MemberId);
    }

    [Fact]
    public void CappedSplit_MultipleCappedAndUncapped()
    {
        // Total = 300 split equally three ways → raw share 100 each.
        // Frank: cap=40, raw=100 → fair share=40, excess=60
        // Grace: cap=60, raw=100 → fair share=60, excess=40
        // Henry: no cap, raw=100 → absorbs both excesses: 100+60+40=200
        var frank = Member("Frank", 40m, 40m, 100m);
        var grace = Member("Grace", 60m, 60m, 100m);
        var henry = Member("Henry", null, 200m, 100m);

        var result = SplitCalculator.Calculate(new[] { frank, grace, henry });

        result.MemberBalances.Should().AllSatisfy(b => b.Balance.Should().Be(0m));
        result.Settlements.Should().BeEmpty();
    }

    [Fact]
    public void CappedSplit_RawShareBelowCap_CapNotApplied()
    {
        // Dave has a generous $50 cap but only participates in a $20 expense split with Eve,
        // so his real fair share (10) never reaches the cap — he owes exactly his raw share,
        // not an amount bumped up to match the cap.
        var dave = Member("Dave", 50m, 0m, 10m);
        var eve  = Member("Eve",  null, 20m, 10m);

        var result = SplitCalculator.Calculate(new[] { dave, eve });

        result.MemberBalances.Single(b => b.MemberId == dave.MemberId).Balance.Should().Be(-10m);
        result.MemberBalances.Single(b => b.MemberId == eve.MemberId).Balance.Should().Be(10m);

        result.Settlements.Should().HaveCount(1);
        result.Settlements[0].FromMemberId.Should().Be(dave.MemberId);
        result.Settlements[0].ToMemberId.Should().Be(eve.MemberId);
        result.Settlements[0].Amount.Should().Be(10m);
    }

    [Fact]
    public void CappedSplit_WithUnequalRawShares_ExcessRedistributedEquallyAmongUncapped()
    {
        // Frank capped at 15 but his real participation gives him a raw share of 25 (10 over
        // cap). Grace and Henry are uncapped with raw shares of 20 each; they split Frank's
        // 10 excess equally (+5 each).
        var frank = Member("Frank", 15m, 25m, 25m);
        var grace = Member("Grace", null, 20m, 20m);
        var henry = Member("Henry", null, 20m, 20m);

        var result = SplitCalculator.Calculate(new[] { frank, grace, henry });

        result.MemberBalances.Single(b => b.MemberId == frank.MemberId).Balance.Should().Be(10m);
        result.MemberBalances.Single(b => b.MemberId == grace.MemberId).Balance.Should().Be(-5m);
        result.MemberBalances.Single(b => b.MemberId == henry.MemberId).Balance.Should().Be(-5m);

        result.MemberBalances.Sum(b => b.Balance).Should().Be(0m);
    }

    // ─────────────────────────────────────────────────
    // Caps Unenforceable — no uncapped member to absorb excess
    // ─────────────────────────────────────────────────

    [Fact]
    public void CappedSplit_AllMembersCapped_FallsBackToRawShares()
    {
        // Both members are capped at 10, but their raw share (30 each, from an equal
        // $60 split, which A paid in full) exceeds that. With no uncapped member to absorb
        // the excess, caps are unenforceable and everyone just owes their raw share.
        var a = Member("A", 10m, 60m, 30m);
        var b = Member("B", 10m,  0m, 30m);

        var result = SplitCalculator.Calculate(new[] { a, b });

        result.MemberBalances.Single(x => x.MemberId == a.MemberId).Balance.Should().Be(30m);
        result.MemberBalances.Single(x => x.MemberId == b.MemberId).Balance.Should().Be(-30m);
    }

    [Fact]
    public void CappedSplit_CapsExceedRawShare_FallsBackToEqualSplit()
    {
        // Both capped well above their actual raw share (25 each, from an equal $50 split) —
        // caps never bind, so it behaves like a plain equal split.
        var ivan = Member("Ivan", 80m, 50m, 25m);
        var judy = Member("Judy", 70m,  0m, 25m);

        var result = SplitCalculator.Calculate(new[] { ivan, judy });

        result.MemberBalances.Single(x => x.MemberId == ivan.MemberId).Balance.Should().Be(25m);
        result.MemberBalances.Single(x => x.MemberId == judy.MemberId).Balance.Should().Be(-25m);

        result.Settlements.Should().HaveCount(1);
        result.Settlements[0].Amount.Should().Be(25m);
        result.Settlements[0].FromMemberId.Should().Be(judy.MemberId);
        result.Settlements[0].ToMemberId.Should().Be(ivan.MemberId);
    }

    // ─────────────────────────────────────────────────
    // Rounding
    // ─────────────────────────────────────────────────

    [Fact]
    public void EqualSplit_Rounding_RemainderAssignedToFirstMember()
    {
        // Total = 10 split three ways: 10/3 = 3.333..., remainder assigned to the first
        // member — mirrors BuildExpenseSplits' persisted-split rounding convention.
        var shares = EqualRawShares(10m, 3);
        var a = Member("A", null, 10m, shares[0]);
        var b = Member("B", null,  0m, shares[1]);
        var c = Member("C", null,  0m, shares[2]);

        var result = SplitCalculator.Calculate(new[] { a, b, c });

        var totalBalance = result.MemberBalances.Sum(x => x.Balance);
        totalBalance.Should().BeApproximately(0m, 0.001m);

        decimal totalSettled = result.Settlements.Sum(s => s.Amount);
        decimal totalOwed    = result.MemberBalances.Where(x => x.Balance > 0).Sum(x => x.Balance);
        totalSettled.Should().BeApproximately(totalOwed, 0.01m);
    }

    [Fact]
    public void CappedSplit_Rounding_RemainderAssignedToFirstUncappedMember()
    {
        // Total = 100 split three ways: 33.34 / 33.33 / 33.33 (remainder to first).
        var shares = EqualRawShares(100m, 3);
        var capped    = Member("Capped",    1m,  1m, shares[0]);
        var uncapped1 = Member("Uncapped1", null, 0m, shares[1]);
        var uncapped2 = Member("Uncapped2", null, 99m, shares[2]);

        var result = SplitCalculator.Calculate(new[] { capped, uncapped1, uncapped2 });

        var totalBalance = result.MemberBalances.Sum(x => x.Balance);
        totalBalance.Should().BeApproximately(0m, 0.001m);
    }

    // ─────────────────────────────────────────────────
    // Creditor Paid More Than Cap
    // ─────────────────────────────────────────────────

    [Fact]
    public void CappedSplit_MemberPaidMoreThanCap_TreatedAsCreditor()
    {
        // Total = 100 split equally → raw share 50 each.
        // Kate: cap=20, paid=80 (over cap). fair share=20, balance=+60 (creditor).
        // Liam: no cap, paid=20. Absorbs Kate's 30 excess: fair share=50+30=80, balance=-60.
        var kate = Member("Kate", 20m, 80m, 50m);
        var liam = Member("Liam", null, 20m, 50m);

        var result = SplitCalculator.Calculate(new[] { kate, liam });

        var kateBalance = result.MemberBalances.Single(b => b.MemberId == kate.MemberId);
        var liamBalance = result.MemberBalances.Single(b => b.MemberId == liam.MemberId);

        kateBalance.Balance.Should().Be(60m);
        kateBalance.IsCreditor.Should().BeTrue();

        liamBalance.Balance.Should().Be(-60m);
        liamBalance.IsCreditor.Should().BeFalse();

        result.Settlements.Should().HaveCount(1);
        result.Settlements[0].FromMemberId.Should().Be(liam.MemberId);
        result.Settlements[0].ToMemberId.Should().Be(kate.MemberId);
        result.Settlements[0].Amount.Should().Be(60m);
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
        var solo = Member("Solo", null, 100m, 100m);

        var result = SplitCalculator.Calculate(new[] { solo });

        result.MemberBalances.Should().HaveCount(1);
        result.Settlements.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_ZeroTotalSpent_AllBalancesZero()
    {
        var a = Member("A", null, 0m, 0m);
        var b = Member("B", null, 0m, 0m);

        var result = SplitCalculator.Calculate(new[] { a, b });

        result.MemberBalances.Should().AllSatisfy(m => m.Balance.Should().Be(0m));
        result.Settlements.Should().BeEmpty();
    }

    [Fact]
    public void EqualSplit_AllMembersPaidEqual_NoSettlements()
    {
        // Each paid exactly their share → no debts
        var a = Member("A", null, 30m, 30m);
        var b = Member("B", null, 30m, 30m);
        var c = Member("C", null, 30m, 30m);

        var result = SplitCalculator.Calculate(new[] { a, b, c });

        result.MemberBalances.Should().AllSatisfy(m => m.Balance.Should().Be(0m));
        result.Settlements.Should().BeEmpty();
    }

    [Fact]
    public void SimplifyDebts_MultipleCreditors_TransactionsMinimized()
    {
        // Alice: +30, Bob: +10, Carol: -20, Dave: -20
        // Greedy: Carol pays Alice 20, Dave pays Alice 10, Dave pays Bob 10 → 3 transactions
        var alice = new MemberInput(Guid.NewGuid(), "Alice", null, 60m, 30m); // paid 60, share=30 → +30
        var bob   = new MemberInput(Guid.NewGuid(), "Bob",   null, 40m, 30m); // paid 40, share=30 → +10
        var carol = new MemberInput(Guid.NewGuid(), "Carol", null, 10m, 30m); // paid 10, share=30 → -20
        var dave  = new MemberInput(Guid.NewGuid(), "Dave",  null, 10m, 30m); // paid 10, share=30 → -20

        var result = SplitCalculator.Calculate(new[] { alice, bob, carol, dave });

        result.MemberBalances.Sum(b => b.Balance).Should().BeApproximately(0m, 0.001m);

        decimal totalSettled = result.Settlements.Sum(s => s.Amount);
        decimal totalOwed    = result.MemberBalances.Where(b => b.Balance > 0).Sum(b => b.Balance);
        totalSettled.Should().BeApproximately(totalOwed, 0.001m);
    }
}
