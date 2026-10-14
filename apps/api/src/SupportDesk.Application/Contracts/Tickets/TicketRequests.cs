using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>Body of POST /api/tickets.</summary>
public sealed record CreateTicketRequest(
    string Title,
    string Description,
    int CustomerId,
    int CategoryId,
    TicketPriority? RequestedPriority);

/// <summary>Body of PATCH /api/tickets/{id}/status.</summary>
public sealed record UpdateTicketStatusRequest(TicketStatus Status);

/// <summary>Body of PATCH /api/tickets/{id}/assignment. A null agent unassigns the ticket.</summary>
public sealed record AssignTicketRequest(int? AgentId);

/// <summary>
/// Body of POST /api/tickets/{id}/escalate. It carries no "who": the agent who escalates is the
/// one the sign-in token names, so a caller cannot record the escalation under someone else.
/// </summary>
/// <param name="Reason">Why the ticket is being escalated, 5 to 500 characters.</param>
public sealed record EscalateTicketRequest(string Reason);
