using FairShare.Application.DTOs.Auth;
using FairShare.Application.Interfaces;

namespace FairShare.Application.UseCases.Auth;

/// <summary>Result codes for the token-refresh operation.</summary>
public enum RefreshResult { Success, InvalidToken }

/// <summary>
/// Validates an opaque refresh token, rotates it, and issues a new token pair.
/// The old refresh token is invalidated after use.
/// </summary>
public class RefreshTokenUseCase
{
    private readonly IUserRepository _users;
    private readonly ITokenService _tokenService;

    public RefreshTokenUseCase(IUserRepository users, ITokenService tokenService)
    {
        _users        = users;
        _tokenService = tokenService;
    }

    /// <summary>Executes the use case.</summary>
    public async Task<(RefreshResult Result, AuthResponse? Response)> ExecuteAsync(
        RefreshTokenRequest request,
        CancellationToken ct = default)
    {
        // We must locate the user whose stored hash matches the plain token.
        // Since tokens are random and hashed, we search by hash.
        var hash = _tokenService.HashRefreshToken(request.RefreshToken);
        var user = await _users.GetByRefreshTokenHashAsync(hash, ct);

        if (user is null
            || user.RefreshTokenExpiry is null
            || user.RefreshTokenExpiry < DateTime.UtcNow)
        {
            return (RefreshResult.InvalidToken, null);
        }

        // Rotate refresh token
        var newRefreshToken  = _tokenService.GenerateRefreshToken();
        var newRefreshExpiry = DateTime.UtcNow.AddDays(7);

        user.RefreshToken       = _tokenService.HashRefreshToken(newRefreshToken);
        user.RefreshTokenExpiry = newRefreshExpiry;

        _users.Update(user);
        await _users.SaveChangesAsync(ct);

        var accessToken = _tokenService.GenerateAccessToken(user);

        return (RefreshResult.Success, new AuthResponse(
            accessToken,
            newRefreshToken,
            newRefreshExpiry.ToString("o")));
    }
}
