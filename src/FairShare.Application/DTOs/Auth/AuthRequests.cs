namespace FairShare.Application.DTOs.Auth;

/// <summary>Request body for user registration.</summary>
public record RegisterRequest(string Name, string Email, string Password);

/// <summary>Request body for user login.</summary>
public record LoginRequest(string Email, string Password);

/// <summary>Request body for token refresh.</summary>
public record RefreshTokenRequest(string RefreshToken);
