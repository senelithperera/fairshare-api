namespace FairShare.Application.DTOs.Auth;

/// <summary>Response returned after successful login or token refresh.</summary>
public record AuthResponse(string AccessToken, string RefreshToken, string ExpiresAt);
