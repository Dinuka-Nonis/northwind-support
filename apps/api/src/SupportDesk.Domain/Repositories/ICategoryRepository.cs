using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Access to <see cref="Category"/> aggregates.
/// </summary>
public interface ICategoryRepository
{
    /// <summary>
    /// Loads a category to read from (it is not tracked), or null when it does not exist. The
    /// triage rules need its flags, so an existence check alone is no longer enough.
    /// </summary>
    Task<Category?> GetByIdAsync(int id, CancellationToken ct);
}
