namespace SupportDesk.Domain.Triage;

/// <summary>
/// What the assignment rules need to know about one agent: whether they can work, how full they
/// are, and which categories they specialise in. A plain snapshot, so the rules stay pure and
/// the application layer is free to load it with a single query.
/// </summary>
/// <param name="Id">The agent's id. Also the tie-break: the lowest id wins.</param>
/// <param name="Name">The agent's full name, used in reasons.</param>
/// <param name="IsActive">Inactive agents take no work.</param>
/// <param name="MaxOpenTickets">The most open tickets the agent will hold.</param>
/// <param name="OpenTickets">Tickets assigned to the agent that are not Resolved or Closed.</param>
/// <param name="SpecializationCategoryIds">The categories the agent is qualified for.</param>
public sealed record AgentCandidate(
    int Id,
    string Name,
    bool IsActive,
    int MaxOpenTickets,
    int OpenTickets,
    IReadOnlyCollection<int> SpecializationCategoryIds)
{
    /// <summary>True while the agent is strictly below their limit.</summary>
    public bool HasCapacity => OpenTickets < MaxOpenTickets;

    public bool IsQualifiedFor(int categoryId) => SpecializationCategoryIds.Contains(categoryId);
}
