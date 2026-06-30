namespace FairShare.Application.DTOs;

/// <summary>Standard wrapper for list responses: <c>{ data, count }</c>.</summary>
/// <typeparam name="T">The item type.</typeparam>
public record PagedResponse<T>(IReadOnlyList<T> Data, int Count)
{
    /// <summary>Creates a <see cref="PagedResponse{T}"/> from any enumerable.</summary>
    public static PagedResponse<T> From(IReadOnlyList<T> items) => new(items, items.Count);
}
