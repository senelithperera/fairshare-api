using FairShare.Application;
using FairShare.Application.DTOs.Expenses;
using FairShare.Application.DTOs.Groups;
using FairShare.Application.Interfaces;
using FairShare.Application.UseCases.Expenses;
using FairShare.Application.UseCases.Groups;
using FairShare.Application.Validators;
using FairShare.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FairShare.Tests.Application;

/// <summary>
/// Limits on how big a group (and one expense's lists) may get, so a shared link or a script
/// can't grow a group without bound.
/// </summary>
public class GroupLimitsTests
{
    private static readonly Guid GroupId    = Guid.NewGuid();
    private static readonly Guid GuestToken = Guid.NewGuid();
    private static readonly Guid AliceId    = Guid.NewGuid();

    // ── Configuration ─────────────────────────────────────────────────────────

    [Fact]
    public void Defaults_FitAFriendGroupWithRoomToSpare()
    {
        GroupLimits.Default.MaxMembersPerGroup.Should().Be(30);
        GroupLimits.Default.MaxExpensesPerGroup.Should().Be(1000);
    }

    [Fact]
    public void From_UsesDefaultsWhenNotConfigured()
    {
        GroupLimits.From(null, null).Should().Be(GroupLimits.Default);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(500, GroupLimits.PeopleCeiling)]
    [InlineData(12, 12)]
    public void From_KeepsTheMemberLimitWithinBounds(int configured, int expected)
    {
        GroupLimits.From(configured, null).MaxMembersPerGroup.Should().Be(expected);
    }

    // ── Members ───────────────────────────────────────────────────────────────

    private static (Mock<IGroupRepository>, Mock<IGroupMemberRepository>, Mock<IExpenseRepository>) Repos(
        int memberCount = 1, int expenseCount = 0)
    {
        var groups   = new Mock<IGroupRepository>();
        var members  = new Mock<IGroupMemberRepository>();
        var expenses = new Mock<IExpenseRepository>();

        groups.Setup(g => g.GetByGuestTokenAsync(GuestToken, It.IsAny<CancellationToken>()))
              .ReturnsAsync(new Group { Id = GroupId, GuestToken = GuestToken, Name = "Trip", Currency = "LKR" });
        members.Setup(m => m.CountByGroupIdAsync(GroupId, It.IsAny<CancellationToken>())).ReturnsAsync(memberCount);
        members.Setup(m => m.GetByGroupIdAsync(GroupId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<GroupMember> { new() { Id = AliceId, GroupId = GroupId, GuestName = "Alice" } });
        expenses.Setup(e => e.CountByGroupIdAsync(GroupId, It.IsAny<CancellationToken>())).ReturnsAsync(expenseCount);

        return (groups, members, expenses);
    }

    private static AddMemberUseCase AddMember(Mock<IGroupRepository> groups, Mock<IGroupMemberRepository> members) =>
        new(groups.Object, members.Object, NullLogger<AddMemberUseCase>.Instance,
            new GroupLimits { MaxMembersPerGroup = 3 });

    [Fact]
    public async Task AddMember_BelowTheLimit_AddsThem()
    {
        var (groups, members, _) = Repos(memberCount: 2);

        var result = await AddMember(groups, members).ExecuteAsync(GuestToken, new AddMemberRequest("Nimal"));

        result.Status.Should().Be(AddMemberStatus.Success);
        result.Member!.DisplayName.Should().Be("Nimal");
        members.Verify(m => m.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddMember_AtTheLimit_IsRefusedWithoutSaving()
    {
        var (groups, members, _) = Repos(memberCount: 3);

        var result = await AddMember(groups, members).ExecuteAsync(GuestToken, new AddMemberRequest("Nimal"));

        result.Status.Should().Be(AddMemberStatus.GroupFull);
        members.Verify(m => m.AddAsync(It.IsAny<GroupMember>(), It.IsAny<CancellationToken>()), Times.Never);
        members.Verify(m => m.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddMember_UnknownGroup_IsNotFound()
    {
        var (groups, members, _) = Repos();

        var result = await AddMember(groups, members).ExecuteAsync(Guid.NewGuid(), new AddMemberRequest("Nimal"));

        result.Status.Should().Be(AddMemberStatus.GroupNotFound);
    }

    // ── Expenses ──────────────────────────────────────────────────────────────

    private static CreateExpenseUseCase CreateExpense(
        Mock<IGroupRepository> groups, Mock<IGroupMemberRepository> members, Mock<IExpenseRepository> expenses) =>
        new(groups.Object, members.Object, expenses.Object, NullLogger<CreateExpenseUseCase>.Instance,
            new GroupLimits { MaxExpensesPerGroup = 5 });

    private static CreateExpenseRequest Dinner() =>
        new("Dinner", 100, [new PayerRequest(AliceId.ToString(), 100)]);

    [Fact]
    public async Task CreateExpense_BelowTheLimit_AddsIt()
    {
        var (groups, members, expenses) = Repos(expenseCount: 4);

        var result = await CreateExpense(groups, members, expenses).ExecuteAsync(GuestToken, Dinner());

        result.Status.Should().Be(ExpenseResultStatus.Success);
    }

    [Fact]
    public async Task CreateExpense_AtTheLimit_IsRefusedWithoutSaving()
    {
        var (groups, members, expenses) = Repos(expenseCount: 5);

        var result = await CreateExpense(groups, members, expenses).ExecuteAsync(GuestToken, Dinner());

        result.Status.Should().Be(ExpenseResultStatus.GroupFull);
        expenses.Verify(e => e.AddAsync(It.IsAny<Expense>(), It.IsAny<CancellationToken>()), Times.Never);
        expenses.Verify(e => e.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Validators: list lengths ──────────────────────────────────────────────

    private static List<string> Ids(int count) => Enumerable.Range(0, count).Select(_ => Guid.NewGuid().ToString()).ToList();

    [Fact]
    public void CreateValidator_RejectsMoreParticipantsThanAnyGroupCanHave()
    {
        var request = Dinner() with { ParticipantMemberIds = Ids(GroupLimits.PeopleCeiling + 1) };

        var result = new CreateExpenseValidator().Validate(request);

        result.Errors.Should().Contain(e => e.PropertyName == "ParticipantMemberIds");
    }

    [Fact]
    public void CreateValidator_AcceptsParticipantsUpToTheCeiling()
    {
        var request = Dinner() with { ParticipantMemberIds = Ids(GroupLimits.PeopleCeiling) };

        new CreateExpenseValidator().Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateValidator_RejectsMorePayersThanAnyGroupCanHave()
    {
        var payers = Ids(GroupLimits.PeopleCeiling + 1).Select(id => new PayerRequest(id, 1)).ToList();
        var request = new CreateExpenseRequest("Dinner", payers.Count, payers);

        var result = new CreateExpenseValidator().Validate(request);

        result.Errors.Should().Contain(e => e.PropertyName == "Payers");
    }

    [Fact]
    public void UpdateValidator_RejectsTooManyParticipantsAndPayers()
    {
        var payers  = Ids(GroupLimits.PeopleCeiling + 1).Select(id => new PayerRequest(id, 1)).ToList();
        var request = new UpdateExpenseRequest(null, null, payers, Ids(GroupLimits.PeopleCeiling + 1));

        var result = new UpdateExpenseValidator().Validate(request);

        result.Errors.Should().Contain(e => e.PropertyName == "Payers");
        result.Errors.Should().Contain(e => e.PropertyName == "ParticipantMemberIds");
    }
}
