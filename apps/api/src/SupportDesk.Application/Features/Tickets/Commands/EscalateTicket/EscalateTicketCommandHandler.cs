using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Triage;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Raises a ticket's priority one level, recomputes its due date from now, re-evaluates its
/// owner, and records the change in the escalation history. The rules themselves live on the
/// <c>Ticket</c> aggregate and in the triage rules; this handler only loads, calls them, saves
/// and maps.
/// </summary>
public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    TicketTriager triager,
    ITicketQueries ticketQueries,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ILogger<EscalateTicketCommandHandler> logger)
{
    /// <param name="id">The ticket to escalate.</param>
    /// <param name="request">The reason.</param>
    /// <param name="escalatedBy">
    /// Who is escalating it. The caller passes the signed-in agent's name, taken from the token,
    /// so it never comes from the request body.
    /// </param>
    /// <param name="ct">Cancels the work if the caller goes away.</param>
    /// <exception cref="NotFoundException">The ticket (or its customer or category) does not exist.</exception>
    /// <exception cref="BusinessRuleViolationException">
    /// The ticket is Resolved or Closed, or is already Critical. Nothing is saved.
    /// </exception>
    public async Task<EscalationResultDto> HandleAsync(
        int id,
        EscalateTicketRequest request,
        string escalatedBy,
        CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(id, ct) ?? throw new NotFoundException("Ticket", id);

        // Asked first, so a ticket that cannot be escalated is rejected before anything else is
        // loaded, and the message names the real reason.
        var newPriority = ticket.PriorityAfterEscalation();

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);

        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        var now = clock.UtcNow;

        var triage = await triager.TriageEscalationAsync(
            newPriority,
            customer.Tier,
            ticket.CategoryId,
            category.RequiresSpecialist,
            ticket.AssignedAgentId,
            now,
            ct);

        var escalation = ticket.Escalate(
            request.Reason,
            escalatedBy,
            triage.Sla.DueAtUtc,
            triage.Assignment.AgentId,
            now);

        // The ticket and its history row are saved together, or not at all.
        await tickets.AddEscalationAsync(escalation, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} escalated from {FromPriority} to {ToPriority} by {EscalatedBy}; agent {FromAgentId} -> {ToAgentId}.",
            ticket.Reference, escalation.FromPriority, escalation.ToPriority, escalation.EscalatedBy,
            escalation.FromAgentId, escalation.ToAgentId);

        // Both are read back as saved, so the response is exactly what the next GET would show.
        var detail = await getTicket.HandleAsync(id, ct);

        var recorded = (await ticketQueries.GetEscalationsAsync(id, ct))?.FirstOrDefault(e => e.Id == escalation.Id)
            ?? throw new InvalidOperationException(
                $"Escalation {escalation.Id} of ticket {ticket.Reference} was saved but could not be read back.");

        return new EscalationResultDto(detail, recorded, triage.Sla.Reason, triage.Assignment.Reason);
    }
}
