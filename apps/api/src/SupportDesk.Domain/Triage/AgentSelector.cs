namespace SupportDesk.Domain.Triage;

/// <summary>
/// BR-4 and BR-5: picks the agent for a ticket. Pure - it only looks at the candidates it is given.
/// </summary>
public static class AgentSelector
{
    /// <summary>
    /// Picks the eligible agent with the fewest open tickets. Eligible means active, qualified
    /// when the category requires a specialist, and strictly below their limit. Ties go to the
    /// lowest agent id, so the same input always gives the same answer. When nobody is eligible
    /// the ticket stays unassigned, which is a normal outcome and not an error.
    /// </summary>
    public static AssignmentDecision Select(
        IEnumerable<AgentCandidate> candidates,
        int categoryId,
        bool requiresSpecialist)
    {
        var eligible = candidates
            .Where(a => a.IsActive && a.HasCapacity && (!requiresSpecialist || a.IsQualifiedFor(categoryId)))
            .OrderBy(a => a.OpenTickets)
            .ThenBy(a => a.Id)
            .ToList();

        if (eligible.Count == 0)
        {
            return AssignmentDecision.Unassigned(requiresSpecialist
                ? "Left unassigned: no active specialist with spare capacity for this category."
                : "Left unassigned: no active agent with spare capacity.");
        }

        var chosen = eligible[0];
        var pool = requiresSpecialist ? "specialist" : "agent";
        var plural = eligible.Count == 1 ? pool : pool + "s";

        return AssignmentDecision.To(
            chosen,
            $"Assigned to {chosen.Name}: fewest open tickets ({chosen.OpenTickets}) among {eligible.Count} eligible {plural}.");
    }

    /// <summary>
    /// For escalation: keeps the current agent while they are still active and qualified. Capacity
    /// is not checked, because the ticket already counts towards their load - checking it would
    /// make a full agent lose a ticket just because it became more urgent. Otherwise the ticket
    /// is assigned from scratch, exactly as on creation.
    /// </summary>
    public static AssignmentDecision SelectForEscalation(
        int? currentAgentId,
        IReadOnlyCollection<AgentCandidate> candidates,
        int categoryId,
        bool requiresSpecialist)
    {
        var current = currentAgentId is { } id ? candidates.FirstOrDefault(a => a.Id == id) : null;

        if (current is not null && current.IsActive && (!requiresSpecialist || current.IsQualifiedFor(categoryId)))
        {
            return AssignmentDecision.To(current, $"Kept {current.Name}: still active and qualified for this ticket.");
        }

        var fresh = Select(candidates, categoryId, requiresSpecialist);

        if (currentAgentId is null)
        {
            return fresh;
        }

        return fresh with { Reason = $"The previous agent can no longer take this ticket. {fresh.Reason}" };
    }
}

/// <summary>The agent chosen for a ticket (or none) and a plain-language reason.</summary>
public sealed record AssignmentDecision(AgentCandidate? Agent, string Reason)
{
    public int? AgentId => Agent?.Id;

    public static AssignmentDecision To(AgentCandidate agent, string reason) => new(agent, reason);

    public static AssignmentDecision Unassigned(string reason) => new(null, reason);
}
