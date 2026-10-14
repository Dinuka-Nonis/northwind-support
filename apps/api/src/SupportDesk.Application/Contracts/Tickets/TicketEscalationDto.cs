using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// One row of a ticket's escalation history: what the priority, owner and due date were before
/// and after, why, who did it and when. Agents are labelled with their names.
/// </summary>
public sealed record TicketEscalationDto(
    int Id,
    TicketPriority FromPriority,
    TicketPriority ToPriority,
    DateTime? FromDueAtUtc,
    DateTime ToDueAtUtc,
    AgentSummaryDto? FromAgent,
    AgentSummaryDto? ToAgent,
    string Reason,
    string EscalatedBy,
    DateTime EscalatedAtUtc);

/// <summary>
/// The response to escalating a ticket: the ticket as it is now, the history row that was just
/// written, and how the new due date and the owner were decided.
/// </summary>
/// <param name="Ticket">The updated ticket.</param>
/// <param name="Escalation">The recorded escalation, as it reads back from the history.</param>
/// <param name="SlaReason">How the new due date was worked out.</param>
/// <param name="AssignmentReason">Whether the owner was kept, replaced, or nobody was eligible.</param>
public sealed record EscalationResultDto(
    TicketDetailDto Ticket,
    TicketEscalationDto Escalation,
    string SlaReason,
    string AssignmentReason);
