using FairShare.Application.Interfaces;
using FairShare.Application.UseCases.Groups;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FairShare.Tests.Application;

/// <summary>Deleting a group is permanent and goes through the group's link (guest token).</summary>
public class DeleteGroupTests
{
    private static readonly Guid GuestToken = Guid.NewGuid();

    private static (DeleteGroupUseCase, Mock<IGroupRepository>) Create(bool exists)
    {
        var groups = new Mock<IGroupRepository>();
        groups.Setup(g => g.DeleteByGuestTokenAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((Guid token, CancellationToken _) => exists && token == GuestToken);
        return (new DeleteGroupUseCase(groups.Object, NullLogger<DeleteGroupUseCase>.Instance), groups);
    }

    [Fact]
    public async Task ExistingGroup_IsDeleted()
    {
        var (useCase, groups) = Create(exists: true);

        (await useCase.ExecuteAsync(GuestToken)).Should().BeTrue();
        groups.Verify(g => g.DeleteByGuestTokenAsync(GuestToken, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnknownOrAlreadyDeletedGroup_IsNotFound()
    {
        var (useCase, _) = Create(exists: false);

        (await useCase.ExecuteAsync(GuestToken)).Should().BeFalse();
    }
}
