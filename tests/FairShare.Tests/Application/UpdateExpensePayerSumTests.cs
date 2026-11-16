using FairShare.Application.DTOs.Expenses;
using FairShare.Application.Interfaces;
using FairShare.Application.UseCases.Expenses;
using FairShare.Domain.Entities;
using FluentAssertions;
using Moq;

namespace FairShare.Tests.Application;

/// <summary>
/// A partial update must not leave an expense's payers out of sync with its total. The validator
/// only compares the two when both are sent, so sending just one of them used to save an expense
/// whose payers no longer summed to the total, and group balances stopped netting to zero.
/// </summary>
public class UpdateExpensePayerSumTests
{
    private static readonly Guid GroupId    = Guid.NewGuid();
    private static readonly Guid GuestToken = Guid.NewGuid();
    private static readonly Guid ExpenseId  = Guid.NewGuid();
    private static readonly Guid AliceId    = Guid.NewGuid();
    private static readonly Guid BobId      = Guid.NewGuid();

    /// <summary>Hotel, 300 — Alice paid it all, split between Alice and Bob.</summary>
    private static (UpdateExpenseUseCase, Mock<IExpenseRepository>) UseCase()
    {
        var groups   = new Mock<IGroupRepository>();
        var members  = new Mock<IGroupMemberRepository>();
        var expenses = new Mock<IExpenseRepository>();

        groups.Setup(g => g.GetByGuestTokenAsync(GuestToken, It.IsAny<CancellationToken>()))
              .ReturnsAsync(new Group { Id = GroupId, GuestToken = GuestToken, Name = "Trip", Currency = "LKR" });
        members.Setup(m => m.GetByGroupIdAsync(GroupId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<GroupMember>
               {
                   new() { Id = AliceId, GroupId = GroupId, GuestName = "Alice" },
                   new() { Id = BobId,   GroupId = GroupId, GuestName = "Bob" }
               });
        expenses.Setup(e => e.GetByIdAsync(ExpenseId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Expense
                {
                    Id          = ExpenseId,
                    GroupId     = GroupId,
                    Title       = "Hotel",
                    TotalAmount = 300,
                    Payers      = [new ExpensePayer { Id = Guid.NewGuid(), ExpenseId = ExpenseId, MemberId = AliceId, AmountPaid = 300 }],
                    Splits      =
                    [
                        new ExpenseSplit { Id = Guid.NewGuid(), ExpenseId = ExpenseId, MemberId = AliceId, ShareAmount = 150 },
                        new ExpenseSplit { Id = Guid.NewGuid(), ExpenseId = ExpenseId, MemberId = BobId,   ShareAmount = 150 }
                    ]
                });

        return (new UpdateExpenseUseCase(groups.Object, members.Object, expenses.Object), expenses);
    }

    [Fact]
    public async Task TotalOnly_ThatNoLongerMatchesStoredPayers_IsRejectedWithoutSaving()
    {
        var (useCase, expenses) = UseCase();

        var result = await useCase.ExecuteAsync(GuestToken, ExpenseId,
            new UpdateExpenseRequest(null, 330, null));

        result.Status.Should().Be(ExpenseResultStatus.PayerSumMismatch);
        expenses.Verify(e => e.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PayersOnly_ThatNoLongerMatchStoredTotal_AreRejectedWithoutSaving()
    {
        var (useCase, expenses) = UseCase();

        var result = await useCase.ExecuteAsync(GuestToken, ExpenseId,
            new UpdateExpenseRequest(null, null, [new PayerRequest(AliceId.ToString(), 250)]));

        result.Status.Should().Be(ExpenseResultStatus.PayerSumMismatch);
        expenses.Verify(e => e.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PayersOnly_ThatMatchStoredTotal_AreAccepted()
    {
        var (useCase, _) = UseCase();

        var result = await useCase.ExecuteAsync(GuestToken, ExpenseId,
            new UpdateExpenseRequest(null, null,
                [new PayerRequest(AliceId.ToString(), 200), new PayerRequest(BobId.ToString(), 100)]));

        result.Status.Should().Be(ExpenseResultStatus.Success);
        result.Expense!.Payers.Sum(p => p.AmountPaid).Should().Be(300m);
    }

    [Fact]
    public async Task TitleOnly_KeepsAmountsAndIsAccepted()
    {
        var (useCase, _) = UseCase();

        var result = await useCase.ExecuteAsync(GuestToken, ExpenseId,
            new UpdateExpenseRequest("Hotel (2 nights)", null, null));

        result.Status.Should().Be(ExpenseResultStatus.Success);
        result.Expense!.TotalAmount.Should().Be(300m);
        result.Expense.Splits.Select(s => s.ShareAmount).Should().Equal(150m, 150m);
    }

    [Fact]
    public async Task TotalAndPayersTogether_ThatMatch_AreAccepted()
    {
        var (useCase, _) = UseCase();

        var result = await useCase.ExecuteAsync(GuestToken, ExpenseId,
            new UpdateExpenseRequest(null, 330, [new PayerRequest(AliceId.ToString(), 330)]));

        result.Status.Should().Be(ExpenseResultStatus.Success);
        result.Expense!.Splits.Select(s => s.ShareAmount).Should().Equal(165m, 165m);
    }
}
