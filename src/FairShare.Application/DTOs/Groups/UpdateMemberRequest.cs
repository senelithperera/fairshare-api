namespace FairShare.Application.DTOs.Groups;

/// <summary>Request body for updating a guest member's name or cap (all fields optional).</summary>
public record UpdateMemberRequest(string? GuestName, decimal? BudgetCap);
