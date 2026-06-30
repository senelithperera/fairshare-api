using FairShare.Application.Interfaces;

namespace FairShare.Application.UseCases.Auth;

/// <summary>Invalidates the current user's refresh token.</summary>
public class LogoutUseCase
{
    private readonly IUserRepository _users;

    public LogoutUseCase(IUserRepository users) => _users = users;

    /// <summary>Executes the use case.</summary>
    /// <param name="userId">The authenticated user's ID from JWT claims.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task ExecuteAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null) return;

        user.RefreshToken       = null;
        user.RefreshTokenExpiry = null;

        _users.Update(user);
        await _users.SaveChangesAsync(ct);
    }
}
