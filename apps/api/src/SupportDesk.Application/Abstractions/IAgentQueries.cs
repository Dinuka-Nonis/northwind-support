using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Application.Contracts.Auth;
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

    /// <summary>
    /// The agent with this email, with the password hash the sign-in check needs, or null when
    /// there is none. Reads only the columns the check uses.
    /// </summary>
    Task<AgentLoginRecord?> FindForLoginAsync(string email, CancellationToken ct);
}
