using SupportDesk.Domain.Aggregates.Customers;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Access to <see cref="Customer"/> aggregates.
/// </summary>
public interface ICustomerRepository
{
    /// <summary>
    /// Loads a customer to read from (it is not tracked), or null when it does not exist. The
    /// triage rules need the tier, so an existence check alone is no longer enough.
    /// </summary>
    Task<Customer?> GetByIdAsync(int id, CancellationToken ct);
}
