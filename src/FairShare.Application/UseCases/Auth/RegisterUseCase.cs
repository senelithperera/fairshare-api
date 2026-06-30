using FairShare.Application.DTOs.Auth;
using FairShare.Application.Interfaces;
using FairShare.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace FairShare.Application.UseCases.Auth;

/// <summary>Result codes for the register operation.</summary>
public enum RegisterResult { Success, EmailAlreadyExists }

/// <summary>Registers a new user and returns a token pair.</summary>
public class RegisterUseCase
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly ILogger<RegisterUseCase> _logger;

    public RegisterUseCase(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        ILogger<RegisterUseCase> logger)
    {
        _users          = users;
        _passwordHasher = passwordHasher;
        _tokenService   = tokenService;
        _logger         = logger;
    }

    /// <summary>Executes the use case.</summary>
    public async Task<(RegisterResult Result, AuthResponse? Response)> ExecuteAsync(
        RegisterRequest request,
        CancellationToken ct = default)
    {
        if (await _users.ExistsAsync(request.Email, ct))
            return (RegisterResult.EmailAlreadyExists, null);

        var refreshToken  = _tokenService.GenerateRefreshToken();
        var refreshExpiry = DateTime.UtcNow.AddDays(7);

        var user = new User
        {
            Id                 = Guid.NewGuid(),
            Name               = request.Name.Trim(),
            Email              = request.Email.ToLowerInvariant(),
            PasswordHash       = _passwordHasher.Hash(request.Password),
            RefreshToken       = _tokenService.HashRefreshToken(refreshToken),
            RefreshTokenExpiry = refreshExpiry,
            CreatedAt          = DateTime.UtcNow
        };

        await _users.AddAsync(user, ct);
        await _users.SaveChangesAsync(ct);

        var accessToken = _tokenService.GenerateAccessToken(user);

        _logger.LogInformation("Registered user {UserId} ({Email})", user.Id, user.Email);

        return (RegisterResult.Success, new AuthResponse(
            accessToken,
            refreshToken,
            refreshExpiry.ToString("o")));
    }
}
