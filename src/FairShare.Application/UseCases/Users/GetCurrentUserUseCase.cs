using FairShare.Application.DTOs.Users;
using FairShare.Application.Interfaces;

namespace FairShare.Application.UseCases.Users;

/// <summary>Returns the profile of the currently authenticated user.</summary>
public class GetCurrentUserUseCase
{
    private readonly IUserRepository _users;

    public GetCurrentUserUseCase(IUserRepository users) => _users = users;

    /// <summary>Executes the use case.</summary>
    public async Task<UserResponse?> ExecuteAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null) return null;

        return new UserResponse(
            user.Id.ToString(),
            user.Name,
            user.Email,
            user.CreatedAt.ToString("o"));
    }
}
