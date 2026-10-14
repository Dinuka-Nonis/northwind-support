using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// What the triage rules decided for a new ticket, and why, in plain language. Returned once,
/// when the ticket is created; the ticket itself carries the resulting values.
/// </summary>
/// <param name="Priority">The priority the ticket was given.</param>
/// <param name="PriorityReason">Why that priority (requested, defaulted or forced by the category).</param>
/// <param name="DueAtUtc">The due date that was set.</param>
/// <param name="SlaWindowHours">The response window it was counted from, in hours.</param>
/// <param name="SlaReason">How the window was worked out (base window, Premium, minimum).</param>
/// <param name="AssignedAgent">The agent chosen, or null when nobody was eligible.</param>
/// <param name="AssignmentReason">Why this agent, or why the ticket was left unassigned.</param>
public sealed record TriageDto(
    TicketPriority Priority,
    string PriorityReason,
    DateTime DueAtUtc,
    double SlaWindowHours,
    string SlaReason,
    AgentSummaryDto? AssignedAgent,
    string AssignmentReason);
