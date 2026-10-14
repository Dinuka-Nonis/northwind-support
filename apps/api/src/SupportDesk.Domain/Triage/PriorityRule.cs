using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Triage;

/// <summary>
/// BR-1: decides a new ticket's priority. The category's flag decides, never its name.
/// </summary>
public static class PriorityRule
{
    public const TicketPriority DefaultPriority = TicketPriority.Medium;

    public static PriorityDecision Decide(TicketPriority? requested, bool categoryForcesCritical)
    {
        if (categoryForcesCritical)
        {
            var asked = requested is { } r ? $"{r} was requested" : "none was requested";

            return new PriorityDecision(
                TicketPriority.Critical,
                $"Critical: this category always forces Critical priority ({asked}).");
        }

        return requested is { } priority
            ? new PriorityDecision(priority, $"{priority}: the requested priority was used.")
            : new PriorityDecision(DefaultPriority, $"{DefaultPriority}: no priority was requested, so the default was used.");
    }
}

/// <summary>The priority chosen for a ticket and a plain-language reason.</summary>
public sealed record PriorityDecision(TicketPriority Priority, string Reason);
