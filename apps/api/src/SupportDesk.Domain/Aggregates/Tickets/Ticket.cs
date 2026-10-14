using SupportDesk.Domain.Common;
using SupportDesk.Domain.Exceptions;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// A support request raised by a customer. Every change to a ticket goes through one of its
/// methods, so its rules cannot be bypassed.
/// </summary>
/// <remarks>
/// The customer, the category and the assigned agent are other aggregates and are referenced
/// by id only.
/// </remarks>
public sealed class Ticket : AggregateRoot
{
    private Ticket()
    {
        // For EF Core.
    }

    /// <summary>Human-friendly identifier shown to customers, e.g. TCK-0042.</summary>
    public string Reference { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public int CustomerId { get; private set; }

    public int CategoryId { get; private set; }

    /// <summary>Null while the ticket is waiting for an owner.</summary>
    public int? AssignedAgentId { get; private set; }

    public TicketPriority Priority { get; private set; }

    public TicketStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>When the team has committed to responding by. Null when no SLA applies.</summary>
    public DateTime? DueAtUtc { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }

    /// <summary>True while the ticket still needs work from an agent.</summary>
    public bool IsOpen => Status is not (TicketStatus.Resolved or TicketStatus.Closed);

    /// <summary>
    /// Raises a new ticket for a customer. It starts as <see cref="TicketStatus.New"/>. The due
    /// date and owner are decided by the caller (the triage rules) and passed in; without them
    /// the ticket has no owner and no due date.
    /// </summary>
    public static Ticket Raise(
        string reference,
        string title,
        string description,
        int customerId,
        int categoryId,
        TicketPriority priority,
        DateTime nowUtc,
        DateTime? dueAtUtc = null,
        int? assignedAgentId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new Ticket
        {
            Reference = reference,
            Title = title.Trim(),
            Description = description.Trim(),
            CustomerId = customerId,
            CategoryId = categoryId,
            AssignedAgentId = assignedAgentId,
            Priority = priority,
            Status = TicketStatus.New,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            DueAtUtc = dueAtUtc
        };
    }

    /// <summary>
    /// Moves the ticket through its lifecycle, keeping the resolution timestamp in step.
    /// </summary>
    /// <exception cref="BusinessRuleViolationException">The ticket is closed.</exception>
    public void ChangeStatus(TicketStatus status, DateTime nowUtc)
    {
        if (Status == TicketStatus.Closed && status != TicketStatus.Closed)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is closed and cannot be moved to {status}.");
        }

        ResolvedAtUtc = status switch
        {
            TicketStatus.Resolved => nowUtc,
            TicketStatus.Closed => ResolvedAtUtc ?? nowUtc,
            _ => null
        };

        Status = status;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Hands the ticket to an agent, or takes it back off them when <paramref name="agentId"/>
    /// is null. Whether the agent may take it is checked by the caller, which can see agents.
    /// </summary>
    public void AssignTo(int? agentId, DateTime nowUtc)
    {
        AssignedAgentId = agentId;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// The priority this ticket would have after one escalation. Callers use it to work out the
    /// new due date and owner before calling <see cref="Escalate"/>.
    /// </summary>
    /// <exception cref="BusinessRuleViolationException">
    /// The ticket is Resolved or Closed, or is already Critical. The message says which.
    /// </exception>
    public TicketPriority PriorityAfterEscalation()
    {
        // Checked first so the message names the real reason: a Resolved Critical ticket is
        // rejected for being resolved.
        if (!IsOpen)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is {Status} and cannot be escalated.");
        }

        return Priority switch
        {
            TicketPriority.Low => TicketPriority.Medium,
            TicketPriority.Medium => TicketPriority.High,
            TicketPriority.High => TicketPriority.Critical,
            _ => throw new BusinessRuleViolationException(
                $"Ticket {Reference} is already Critical and cannot be escalated further.")
        };
    }

    /// <summary>
    /// Raises the priority one level and applies the due date and owner that the caller worked out
    /// for it. Returns the history row, which the caller must save with the ticket.
    /// </summary>
    /// <param name="reason">Why, 5 to 500 characters after trimming.</param>
    /// <param name="escalatedBy">Who, 1 to 100 characters after trimming.</param>
    /// <param name="newDueAtUtc">The due date counted from <paramref name="nowUtc"/>.</param>
    /// <param name="newAgentId">The owner after escalation, or null when nobody is eligible.</param>
    /// <exception cref="BusinessRuleViolationException">The ticket cannot be escalated.</exception>
    /// <exception cref="ArgumentException">The reason or the actor is missing or the wrong length.</exception>
    public TicketEscalation Escalate(
        string reason,
        string escalatedBy,
        DateTime newDueAtUtc,
        int? newAgentId,
        DateTime nowUtc)
    {
        var newPriority = PriorityAfterEscalation();

        // The history row is built first, so invalid input leaves the ticket untouched.
        var escalation = new TicketEscalation(
            Id, Priority, newPriority, DueAtUtc, newDueAtUtc, AssignedAgentId, newAgentId, reason, escalatedBy, nowUtc);

        Priority = newPriority;
        DueAtUtc = newDueAtUtc;
        AssignedAgentId = newAgentId;
        UpdatedAtUtc = nowUtc;

        return escalation;
    }
}
