using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Domain.Triage;

namespace SupportDesk.Application.Abstractions;

/// <summary>
/// The read side of agents.
/// </summary>
public interface IAgentQueries
{
    Task<IReadOnlyList<AgentDto>> GetAllAsync(CancellationToken ct);

    /// <summary>
    /// Every agent as the assignment rules need to see them: whether they are active, their
    /// limit, how many open tickets they hold and which categories they specialise in. Loaded
    /// with one query for all agents, so assigning a ticket never costs a query per agent.
    /// </summary>
    Task<IReadOnlyList<AgentCandidate>> GetAssignmentCandidatesAsync(CancellationToken ct);
}
