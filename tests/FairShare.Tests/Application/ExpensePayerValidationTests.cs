using FairShare.Application.DTOs.Expenses;
using FairShare.Application.Interfaces;
using FairShare.Application.UseCases.Expenses;
using FairShare.Application.Validators;
using FairShare.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FairShare.Tests.Application;

/// <summary>
/// Payer member ids must be well-formed, distinct, and belong to the expense's group.
/// Previously these produced a 500 (bad GUID / unknown id) or were silently accepted
/// (duplicates / members of another group).
/// </summary>
public class ExpensePayerValidationTests
{
    private static readonly Guid GroupId    = Guid.NewGuid();
    private static readonly Guid GuestToken = Guid.NewGuid();
    private static readonly Guid AliceId    = Guid.NewGuid();
    private static readonly Guid BobId      = Guid.NewGuid();

    private static CreateExpenseRequest CreateRequest(params PayerRequest[] payers) =>
        new("Dinner", payers.Sum(p => p.AmountPaid), payers);

    // ── Validators ────────────────────────────────────────────────────────────

    [Fact]
    public void Create_RejectsNonGuidPayerId()
    {
        var result = new CreateExpenseValidator().Validate(CreateRequest(new PayerRequest("abc", 100)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Payer member ID must be a valid ID.");
    }

    [Fact]
    public void Create_RejectsDuplicatePayers_IgnoringCase()
    {
        var request = CreateRequest(
            new PayerRequest(AliceId.ToString(), 50),
            new PayerRequest(AliceId.ToString().ToUpperInvariant(), 50));

        var result = new CreateExpenseValidator().Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Each member can only be listed once as a payer.");
    }

    [Fact]
    public void Create_AcceptsDistinctValidPayers()
    {
        var request = CreateRequest(
            new PayerRequest(AliceId.ToString(), 50),
            new PayerRequest(BobId.ToString(), 50));

        new CreateExpenseValidator().Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Update_RejectsNonGuidAndDuplicatePayers()
    {
        var badId = new UpdateExpenseRequest(null, null, [new PayerRequest("not-a-guid", 10)]);
        var dupes = new UpdateExpenseRequest(null, null,
            [new PayerRequest(AliceId.ToString(), 10), new PayerRequest(AliceId.ToString(), 10)]);

        new UpdateExpenseValidator().Validate(badId).IsValid.Should().BeFalse();
        new UpdateExpenseValidator().Validate(dupes).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Update_AllowsOmittingPayers()
    {
        new UpdateExpenseValidator().Validate(new UpdateExpenseRequest("New title", null, null))
            .IsValid.Should().BeTrue();
    }

    // ── Use cases: group membership ───────────────────────────────────────────

    private static (Mock<IGroupRepository>, Mock<IGroupMemberRepository>, Mock<IExpenseRepository>) Repos()
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

        return (groups, members, expenses);
    }

    [Fact]
    public async Task CreateUseCase_RejectsPayerFromAnotherGroup_WithoutSaving()
    {
        var (groups, members, expenses) = Repos();
        var useCase = new CreateExpenseUseCase(groups.Object, members.Object, expenses.Object,
            NullLogger<CreateExpenseUseCase>.Instance);

        var result = await useCase.ExecuteAsync(GuestToken,
            CreateRequest(new PayerRequest(Guid.NewGuid().ToString(), 100)));

        result.Status.Should().Be(ExpenseResultStatus.PayerNotInGroup);
        expenses.Verify(e => e.AddAsync(It.IsAny<Expense>(), It.IsAny<CancellationToken>()), Times.Never);
        expenses.Verify(e => e.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateUseCase_AcceptsGroupMemberPayer()
    {
        var (groups, members, expenses) = Repos();
        var useCase = new CreateExpenseUseCase(groups.Object, members.Object, expenses.Object,
            NullLogger<CreateExpenseUseCase>.Instance);

        var result = await useCase.ExecuteAsync(GuestToken,
            CreateRequest(new PayerRequest(AliceId.ToString(), 100)));

        result.Status.Should().Be(ExpenseResultStatus.Success);
        result.Expense!.Payers.Should().ContainSingle(p => p.DisplayName == "Alice");
    }

    [Fact]
    public async Task UpdateUseCase_RejectsPayerFromAnotherGroup_WithoutSaving()
    {
        var (groups, members, expenses) = Repos();
        var expenseId = Guid.NewGuid();
        expenses.Setup(e => e.GetByIdAsync(expenseId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Expense { Id = expenseId, GroupId = GroupId, Title = "Dinner", TotalAmount = 100 });
        var useCase = new UpdateExpenseUseCase(groups.Object, members.Object, expenses.Object);

        var result = await useCase.ExecuteAsync(GuestToken, expenseId,
            new UpdateExpenseRequest(null, null, [new PayerRequest(Guid.NewGuid().ToString(), 100)]));

        result.Status.Should().Be(ExpenseResultStatus.PayerNotInGroup);
        expenses.Verify(e => e.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
