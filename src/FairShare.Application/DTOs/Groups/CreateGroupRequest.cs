namespace FairShare.Application.DTOs.Groups;

/// <summary>Request body for creating a new group.</summary>
public record CreateGroupRequest(string Name, string Currency = "LKR");
