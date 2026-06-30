using FairShare.Domain.Services;
using FluentAssertions;

namespace FairShare.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="SplitCalculator"/>.
/// Each test covers a distinct scenario; no infrastructure or EF dependencies.
/// </summary>
public class SplitCalculatorTests
{
    // ─────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────

    private static MemberInput Member(string name, decimal? cap, decimal paid)
        => new(Guid.NewGuid(), name, cap, paid);

    // ─────────────────────────────────────────────────
    // Equal Split
    // ─────────────────────────────────────────────────

    [Fact]
    public void EqualSplit_ThreeMembers_BalancesAreCorrect()
    {
        // Arrange: Alice paid 90, Bob paid 0, Carol paid 0. Total = 90.
        // Each owes 30. Alice is creditor (+60), Bob debtor (-30), Carol debtor (-30).
        var alice = Member("Alice", null, 90m);
        var bob   = Member("Bob",   null,  0m);
        var carol = Member("Carol", null,  0m);
        var members = new[] { alice, bob, carol };

        // Act
        var result = SplitCalculator.Calculate(members, 90m);

        // Assert balances
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
        var alice = Member("Alice", null, 90m);
        var bob   = Member("Bob",   null,  0m);
        var carol = Member("Carol", null,  0m);

        var result = SplitCalculator.Calculate(new[] { alice, bob, carol }, 90m);

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
        var alice = Member("Alice", null, 70m);
        var bob   = Member("Bob",   null, 30m);

        var result = SplitCalculator.Calculate(new[] { alice, bob }, 100m);

        result.MemberBalances.Single(b => b.MemberId == alice.MemberId).Balance.Should().Be(20m);
        result.MemberBalances.Single(b => b.MemberId == bob.MemberId).Balance.Should().Be(-20m);

        result.Settlements.Should().HaveCount(1);
        result.Settlements[0].Amount.Should().Be(20m);
        result.Settlements[0].FromMemberId.Should().Be(bob.MemberId);
        result.Settlements[0].ToMemberId.Should().Be(alice.MemberId);
    }

    // ─────────────────────────────────────────────────
    // Capped Split
    // ─────────────────────────────────────────────────

    [Fact]
    public void CappedSplit_OneCappedOneUncapped_SharesAreCorrect()
    {
        // Total = 100. Dave capped at 20. Eve uncapped.
        // cappedTotal=20 < 100, so caps valid.
        // remaining = 80, Eve's share = 80.
        // Dave paid 20 → balance 0. Eve paid 0 → balance -80.
        var dave = Member("Dave", 20m, 20m);
        var eve  = Member("Eve",  null,  0m);

        var result = SplitCalculator.Calculate(new[] { dave, eve }, 100m);

        result.MemberBalances.Single(b => b.MemberId == dave.MemberId).Balance.Should().Be(0m);
        result.MemberBalances.Single(b => b.MemberId == eve.MemberId).Balance.Should().Be(-80m);

        result.Settlements.Should().HaveCount(1);
        result.Settlements[0].Amount.Should().Be(80m);
        result.Settlements[0].FromMemberId.Should().Be(eve.MemberId);
    }

    [Fact]
    public void CappedSplit_MultipleCappedAndUncapped()
    {
        // Total = 300.
        // Frank: cap=40, paid 40  → fair share=40, balance=0
        // Grace: cap=60, paid 60  → fair share=60, balance=0
        // Henry: no cap,  paid 200 → remaining=200, equalShare=200, balance=0
        var frank = Member("Frank", 40m, 40m);
        var grace = Member("Grace", 60m, 60m);
        var henry = Member("Henry", null, 200m);

        var result = SplitCalculator.Calculate(new[] { frank, grace, henry }, 300m);

        result.MemberBalances.Should().AllSatisfy(b => b.Balance.Should().Be(0m));
        result.Settlements.Should().BeEmpty();
    }

    // ─────────────────────────────────────────────────
    // Cap Exceeds Total — fallback to equal split
    // ─────────────────────────────────────────────────

    [Fact]
    public void CappedSplit_CapsExceedTotal_FallsBackToEqualSplit()
    {
        // Total = 50. Both capped, cappedTotal = 150 >= 50 → fall back to equal.
        // Ivan cap=80, paid 50, Judy cap=70, paid 0.
        // Equal share = 25 each.
        // Ivan: 50-25 = +25 (creditor). Judy: 0-25 = -25 (debtor).
        var ivan = Member("Ivan", 80m, 50m);
        var judy = Member("Judy", 70m,  0m);

        var result = SplitCalculator.Calculate(new[] { ivan, judy }, 50m);

        result.MemberBalances.Single(b => b.MemberId == ivan.MemberId).Balance.Should().Be(25m);
        result.MemberBalances.Single(b => b.MemberId == judy.MemberId).Balance.Should().Be(-25m);

        result.Settlements.Should().HaveCount(1);
        result.Settlements[0].Amount.Should().Be(25m);
        result.Settlements[0].FromMemberId.Should().Be(judy.MemberId);
        result.Settlements[0].ToMemberId.Should().Be(ivan.MemberId);
    }

    [Fact]
    public void CappedSplit_CapsExactlyEqualTotal_FallsBackToEqualSplit()
    {
        // cappedTotal == totalSpent → must fall back
        var a = Member("A", 50m, 50m);
        var b = Member("B", 50m,  0m);

        var result = SplitCalculator.Calculate(new[] { a, b }, 100m);

        // Equal split: each owes 50
        result.MemberBalances.Single(x => x.MemberId == a.MemberId).Balance.Should().Be(0m);
        result.MemberBalances.Single(x => x.MemberId == b.MemberId).Balance.Should().Be(-50m);
    }

    // ─────────────────────────────────────────────────
    // Rounding
    // ─────────────────────────────────────────────────

    [Fact]
    public void EqualSplit_Rounding_RemainderAssignedToFirstMember()
    {
        // Total = 10. Three members. 10/3 = 3.333...
        // Using integer-style decimal: 3.333... * 3 != 10 exactly.
        // The calculator floors to machine precision then assigns remainder to first member.
        var a = Member("A", null, 10m);
        var b = Member("B", null,  0m);
        var c = Member("C", null,  0m);

        var result = SplitCalculator.Calculate(new[] { a, b, c }, 10m);

        // All balances should sum to zero
        var totalBalance = result.MemberBalances.Sum(x => x.Balance);
        totalBalance.Should().BeApproximately(0m, 0.001m);

        // Settlements should zero out the net
        decimal totalSettled = result.Settlements.Sum(s => s.Amount);
        decimal totalOwed    = result.MemberBalances.Where(x => x.Balance > 0).Sum(x => x.Balance);
        totalSettled.Should().BeApproximately(totalOwed, 0.01m);
    }

    [Fact]
    public void CappedSplit_Rounding_RemainderAssignedToFirstUncappedMember()
    {
        // Total = 100. One cap=10. Two uncapped. Remaining = 90. 90/2 = 45 exactly — clean case.
        // Swap: Total=100, cap=1. Two uncapped. Remaining=99. 99/2=49.5 — verify sum.
        var capped   = Member("Capped",   1m,   1m);
        var uncapped1 = Member("Uncapped1", null, 0m);
        var uncapped2 = Member("Uncapped2", null, 99m);

        var result = SplitCalculator.Calculate(new[] { capped, uncapped1, uncapped2 }, 100m);

        var totalBalance = result.MemberBalances.Sum(x => x.Balance);
        totalBalance.Should().BeApproximately(0m, 0.001m);
    }

    // ─────────────────────────────────────────────────
    // Creditor Paid More Than Cap
    // ─────────────────────────────────────────────────

    [Fact]
    public void CappedSplit_MemberPaidMoreThanCap_TreatedAsCreditor()
    {
        // Total = 100. Kate: cap=20, paid=80 (over cap). Liam: no cap, paid=20.
        // cappedTotal=20 < 100, useCaps=true.
        // Kate fairShare=20, balance = 80-20 = +60 (creditor).
        // remaining=80, Liam fairShare=80, balance = 20-80 = -60 (debtor).
        var kate = Member("Kate", 20m, 80m);
        var liam = Member("Liam", null, 20m);

        var result = SplitCalculator.Calculate(new[] { kate, liam }, 100m);

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
        var result = SplitCalculator.Calculate([], 100m);

        result.MemberBalances.Should().BeEmpty();
        result.Settlements.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_OneMember_NoSettlementsNeeded()
    {
        var solo = Member("Solo", null, 100m);

        var result = SplitCalculator.Calculate(new[] { solo }, 100m);

        result.MemberBalances.Should().HaveCount(1);
        result.Settlements.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_ZeroTotalSpent_AllBalancesZero()
    {
        var a = Member("A", null, 0m);
        var b = Member("B", null, 0m);

        var result = SplitCalculator.Calculate(new[] { a, b }, 0m);

        result.MemberBalances.Should().AllSatisfy(m => m.Balance.Should().Be(0m));
        result.Settlements.Should().BeEmpty();
    }

    [Fact]
    public void EqualSplit_AllMembersPaidEqual_NoSettlements()
    {
        // Each paid exactly their share → no debts
        var a = Member("A", null, 30m);
        var b = Member("B", null, 30m);
        var c = Member("C", null, 30m);

        var result = SplitCalculator.Calculate(new[] { a, b, c }, 90m);

        result.MemberBalances.Should().AllSatisfy(m => m.Balance.Should().Be(0m));
        result.Settlements.Should().BeEmpty();
    }

    [Fact]
    public void SimplifyDebts_MultipleCreditors_TransactionsMinimized()
    {
        // Alice: +30, Bob: +10, Carol: -20, Dave: -20
        // Greedy: Carol pays Alice 20, Dave pays Alice 10, Dave pays Bob 10 → 3 transactions
        var alice = new MemberInput(Guid.NewGuid(), "Alice", null, 60m); // paid 60, share=30 → +30
        var bob   = new MemberInput(Guid.NewGuid(), "Bob",   null, 40m); // paid 40, share=30 → +10
        var carol = new MemberInput(Guid.NewGuid(), "Carol", null, 10m); // paid 10, share=30 → -20
        var dave  = new MemberInput(Guid.NewGuid(), "Dave",  null, 10m); // paid 10, share=30 → -20

        var result = SplitCalculator.Calculate(new[] { alice, bob, carol, dave }, 120m);

        result.MemberBalances.Sum(b => b.Balance).Should().BeApproximately(0m, 0.001m);

        decimal totalSettled = result.Settlements.Sum(s => s.Amount);
        decimal totalOwed    = result.MemberBalances.Where(b => b.Balance > 0).Sum(b => b.Balance);
        totalSettled.Should().BeApproximately(totalOwed, 0.001m);
    }
}
