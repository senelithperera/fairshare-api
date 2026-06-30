using FairShare.Application.DTOs.Auth;
using FairShare.Application.Interfaces;

namespace FairShare.Application.UseCases.Auth;

/// <summary>Result codes for the login operation.</summary>
public enum LoginResult { Success, InvalidCredentials }

/// <summary>Authenticates a user with email and password and issues a token pair.</summary>
public class LoginUseCase
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public LoginUseCase(IUserRepository users, IPasswordHasher passwordHasher, ITokenService tokenService)
    {
        _users          = users;
        _passwordHasher = passwordHasher;
        _tokenService   = tokenService;
    }

    /// <summary>Executes the use case.</summary>
    public async Task<(LoginResult Result, AuthResponse? Response)> ExecuteAsync(
        LoginRequest request,
        CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(request.Email.ToLowerInvariant(), ct);
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            return (LoginResult.InvalidCredentials, null);

        var refreshToken  = _tokenService.GenerateRefreshToken();
        var refreshExpiry = DateTime.UtcNow.AddDays(7);

        user.RefreshToken       = _tokenService.HashRefreshToken(refreshToken);
        user.RefreshTokenExpiry = refreshExpiry;

        _users.Update(user);
        await _users.SaveChangesAsync(ct);

        var accessToken = _tokenService.GenerateAccessToken(user);

        return (LoginResult.Success, new AuthResponse(
            accessToken,
            refreshToken,
            refreshExpiry.ToString("o")));
    }
}
