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

    /// <summary>
    /// BR-7 for a ticket that is about to be escalated: the due date counted from
    /// <paramref name="nowUtc"/> for its new priority, and its owner afterwards. The current
    /// agent is kept while they are still active and qualified; otherwise the ticket is
    /// reassigned exactly as on creation. An unassigned ticket gets an owner chosen the same way.
    /// </summary>
    public async Task<EscalationTriage> TriageEscalationAsync(
        TicketPriority newPriority,
        CustomerTier tier,
        int categoryId,
        bool requiresSpecialist,
        int? currentAgentId,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var due = sla.DueAt(newPriority, tier, nowUtc);

        var candidates = await agents.GetAssignmentCandidatesAsync(ct);
        var assignment = AgentSelector.SelectForEscalation(currentAgentId, candidates, categoryId, requiresSpecialist);

        return new EscalationTriage(due, assignment);
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

/// <summary>The decision for an escalated ticket: the new due date and the owner, each with its reason.</summary>
public sealed record EscalationTriage(SlaDueDate Sla, AssignmentDecision Assignment);
