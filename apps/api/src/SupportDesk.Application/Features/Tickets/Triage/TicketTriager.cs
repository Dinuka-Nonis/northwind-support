using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Triage;

namespace SupportDesk.Application.Features.Tickets.Triage;

/// <summary>
/// Puts the triage rules together for a use case: it asks the SLA policy for the due date and
/// the agent selector for the owner. It holds no rules of its own - those stay in the domain's
/// <see cref="PriorityRule"/>, <see cref="SlaPolicy"/> and <see cref="AgentSelector"/> - so
/// raising a ticket and escalating one can share it without sharing a handler.
/// </summary>
/// <remarks>
/// It takes plain values (tier, category id and flags) instead of whole aggregates, so a caller
/// passes exactly what it already checked and a test needs no entity with a database id.
/// </remarks>
public sealed class TicketTriager(SlaPolicy sla, IAgentQueries agents)
{
    /// <summary>
    /// BR-1 to BR-6 for a ticket that is about to be raised: its priority, its due date counted
    /// from <paramref name="nowUtc"/>, and its owner. Nobody being eligible is a normal outcome
    /// (the ticket stays unassigned, with a reason), not an error.
    /// </summary>
    public async Task<TicketTriage> TriageNewAsync(
        TicketPriority? requestedPriority,
        CustomerTier tier,
        int categoryId,
        bool requiresSpecialist,
        bool forcesCriticalPriority,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var priority = PriorityRule.Decide(requestedPriority, forcesCriticalPriority);
        var due = sla.DueAt(priority.Priority, tier, nowUtc);

        var candidates = await agents.GetAssignmentCandidatesAsync(ct);
        var assignment = AgentSelector.Select(candidates, categoryId, requiresSpecialist);

        return new TicketTriage(priority, due, assignment);
    }
}

/// <summary>The triage decision for a new ticket: priority, due date and owner, each with its reason.</summary>
public sealed record TicketTriage(PriorityDecision Priority, SlaDueDate Sla, AssignmentDecision Assignment)
{
    /// <summary>The decision as it is returned to the caller.</summary>
    public TriageDto ToDto() => new(
        Priority.Priority,
        Priority.Reason,
        Sla.DueAtUtc,
        Sla.Window.TotalHours,
        Sla.Reason,
        Assignment.Agent is { } agent ? new AgentSummaryDto(agent.Id, agent.Name) : null,
        Assignment.Reason);
}
