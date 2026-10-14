using FluentValidation;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// The same limits the history table and the escalation row enforce, measured after trimming
/// (so a reason of spaces is empty, and padding never counts towards the length). The web
/// form checks the same numbers.
/// </summary>
public sealed class EscalateTicketRequestValidator : AbstractValidator<EscalateTicketRequest>
{
    public EscalateTicketRequestValidator()
    {
        RuleFor(x => x.Reason)
            .Must(reason => TrimmedLength(reason) >= TicketEscalation.ReasonMinLength)
            .WithMessage($"A reason of at least {TicketEscalation.ReasonMinLength} characters is required.")
            .Must(reason => TrimmedLength(reason) <= TicketEscalation.ReasonMaxLength)
            .WithMessage($"The reason must be at most {TicketEscalation.ReasonMaxLength} characters.");

        RuleFor(x => x.EscalatedBy)
            .Must(escalatedBy => TrimmedLength(escalatedBy) >= 1)
            .WithMessage("Who is escalating the ticket is required.")
            .Must(escalatedBy => TrimmedLength(escalatedBy) <= TicketEscalation.EscalatedByMaxLength)
            .WithMessage($"Who is escalating the ticket must be at most {TicketEscalation.EscalatedByMaxLength} characters.");
    }

    private static int TrimmedLength(string? value) => value?.Trim().Length ?? 0;
}
