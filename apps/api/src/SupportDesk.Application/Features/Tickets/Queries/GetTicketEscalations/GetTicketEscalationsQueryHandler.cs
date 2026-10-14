using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;

namespace SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;

/// <summary>
/// Returns a ticket's escalation history, newest first. A ticket that was never escalated has
/// an empty history; a ticket that does not exist is a 404.
/// </summary>
public sealed class GetTicketEscalationsQueryHandler(ITicketQueries tickets)
{
    /// <exception cref="NotFoundException">No ticket has this id.</exception>
    public async Task<IReadOnlyList<TicketEscalationDto>> HandleAsync(int ticketId, CancellationToken ct) =>
        await tickets.GetEscalationsAsync(ticketId, ct) ?? throw new NotFoundException("Ticket", ticketId);
}
