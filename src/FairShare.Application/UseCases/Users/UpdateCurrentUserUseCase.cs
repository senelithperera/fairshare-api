using FairShare.Application.DTOs.Users;
using FairShare.Application.Interfaces;

namespace FairShare.Application.UseCases.Users;

/// <summary>Result codes for the update-user operation.</summary>
public enum UpdateUserResult { Success, NotFound, EmailTaken }

/// <summary>Updates the authenticated user's name or email.</summary>
public class UpdateCurrentUserUseCase
{
    private readonly IUserRepository _users;

    public UpdateCurrentUserUseCase(IUserRepository users) => _users = users;

    /// <summary>Executes the use case.</summary>
    public async Task<(UpdateUserResult Result, UserResponse? Response)> ExecuteAsync(
        Guid userId,
        UpdateUserRequest request,
        CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null) return (UpdateUserResult.NotFound, null);

        if (request.Email is not null && request.Email != user.Email)
        {
            if (await _users.ExistsAsync(request.Email, ct))
                return (UpdateUserResult.EmailTaken, null);

            user.Email = request.Email.ToLowerInvariant();
        }

        if (request.Name is not null)
            user.Name = request.Name.Trim();

        _users.Update(user);
        await _users.SaveChangesAsync(ct);

        return (UpdateUserResult.Success, new UserResponse(
            user.Id.ToString(),
            user.Name,
            user.Email,
            user.CreatedAt.ToString("o")));
    }
}
