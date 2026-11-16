using FairShare.Application.DTOs.Expenses;
using FairShare.Application.DTOs.Groups;
using FairShare.Application.Validators;
using FluentAssertions;

namespace FairShare.Tests.Application;

/// <summary>
/// Updates may omit a member's name or an expense's title (keeping it), but must not blank it
/// out. Previously an empty or whitespace-only value passed validation and was saved as "".
/// </summary>
public class UpdateNameValidationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateMember_RejectsBlankName(string name)
    {
        var result = new UpdateMemberValidator().Validate(new UpdateMemberRequest(name));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "GuestName" && e.ErrorMessage == "Guest name is required.");
    }

    [Fact]
    public void UpdateMember_AllowsOmittingName()
    {
        new UpdateMemberValidator().Validate(new UpdateMemberRequest(null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateMember_AcceptsRealName()
    {
        new UpdateMemberValidator().Validate(new UpdateMemberRequest("Alice K.")).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateExpense_RejectsBlankTitle(string title)
    {
        var result = new UpdateExpenseValidator().Validate(new UpdateExpenseRequest(title, null, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title" && e.ErrorMessage == "Title is required.");
    }

    [Fact]
    public void UpdateExpense_AllowsOmittingTitle()
    {
        new UpdateExpenseValidator().Validate(new UpdateExpenseRequest(null, 100, null)).IsValid.Should().BeTrue();
    }
}
