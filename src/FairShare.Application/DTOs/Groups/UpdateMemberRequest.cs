namespace FairShare.Application.DTOs.Groups;

/// <summary>Request body for updating a guest member (all fields optional).</summary>
public record UpdateMemberRequest(string? GuestName);
