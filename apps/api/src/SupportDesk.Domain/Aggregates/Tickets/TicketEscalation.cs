using SupportDesk.Domain.Common;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// One row of a ticket's escalation history: what changed, why, who did it and when. Written
/// once by <see cref="Ticket.Escalate"/> and never changed afterwards.
/// </summary>
/// <remarks>
/// It belongs to the ticket's history but is not reached through the ticket; it is saved with
/// the ticket in the same unit of work and read back through its own query.
/// </remarks>
public sealed class TicketEscalation : Entity
{
    public const int ReasonMinLength = 5;
    public const int ReasonMaxLength = 500;
    public const int EscalatedByMaxLength = 100;

    private TicketEscalation()
    {
        // For EF Core.
    }

    internal TicketEscalation(
        int ticketId,
        TicketPriority fromPriority,
        TicketPriority toPriority,
        DateTime? fromDueAtUtc,
        DateTime toDueAtUtc,
        int? fromAgentId,
        int? toAgentId,
        string reason,
        string escalatedBy,
        DateTime escalatedAtUtc)
    {
        TicketId = ticketId;
        FromPriority = fromPriority;
        ToPriority = toPriority;
        FromDueAtUtc = fromDueAtUtc;
        ToDueAtUtc = toDueAtUtc;
        FromAgentId = fromAgentId;
        ToAgentId = toAgentId;
        Reason = Clean(reason, nameof(reason), ReasonMinLength, ReasonMaxLength);
        EscalatedBy = Clean(escalatedBy, nameof(escalatedBy), 1, EscalatedByMaxLength);
        EscalatedAtUtc = escalatedAtUtc;
    }

    public int TicketId { get; private set; }

    public TicketPriority FromPriority { get; private set; }

    public TicketPriority ToPriority { get; private set; }

    /// <summary>Null when the ticket had no due date before it was escalated.</summary>
    public DateTime? FromDueAtUtc { get; private set; }

    public DateTime ToDueAtUtc { get; private set; }

    /// <summary>Null when the ticket was unassigned before it was escalated.</summary>
    public int? FromAgentId { get; private set; }

    /// <summary>Null when nobody was eligible to own the ticket after it was escalated.</summary>
    public int? ToAgentId { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    /// <summary>Who escalated it. Free text until Task 3 takes it from the sign-in token.</summary>
    public string EscalatedBy { get; private set; } = string.Empty;

    public DateTime EscalatedAtUtc { get; private set; }

    private static string Clean(string? value, string name, int minLength, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Length < minLength || trimmed.Length > maxLength)
        {
            throw new ArgumentException($"{name} must be {minLength} to {maxLength} characters after trimming.", name);
        }

        return trimmed;
    }
}
