namespace FairShare.Application.DTOs.Users;

/// <summary>Current user profile response.</summary>
public record UserResponse(string UserId, string Name, string Email, string CreatedAt);

/// <summary>Request body for updating the current user's profile.</summary>
public record UpdateUserRequest(string? Name, string? Email);
