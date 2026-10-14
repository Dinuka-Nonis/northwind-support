namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Works out where a ticket stands against its due date. SLA status is always derived from
/// the ticket's dates and the current time - never stored.
/// </summary>
public static class SlaEvaluator
{
    /// <param name="createdAtUtc">When the ticket's SLA window started.</param>
    /// <param name="dueAtUtc">The due date, or null when the ticket has no SLA.</param>
    /// <param name="resolvedAtUtc">When the ticket was resolved, or null while it is open.</param>
    /// <param name="nowUtc">The current time. Passed in so callers and tests control it.</param>
    /// <param name="atRiskFraction">
    /// The share of the window that counts as "close to breaching" (0.25 = the last quarter).
    /// </param>
    /// <remarks>
    /// The window runs from creation to the due date. Every boundary is inclusive towards the
    /// kinder status: resolved exactly on the due date is Met, and exactly at the threshold is
    /// AtRisk. Once now is past the due date, the ticket is Breached.
    /// </remarks>
    public static SlaStatus Evaluate(
        DateTime createdAtUtc,
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc,
        double atRiskFraction)
    {
        if (dueAtUtc is not { } due)
        {
            return SlaStatus.NotApplicable;
        }

        if (resolvedAtUtc is { } resolved)
        {
            return resolved <= due ? SlaStatus.Met : SlaStatus.Breached;
        }

        if (nowUtc > due)
        {
            return SlaStatus.Breached;
        }

        var window = due - createdAtUtc;
        var remaining = due - nowUtc;

        return remaining <= window * atRiskFraction ? SlaStatus.AtRisk : SlaStatus.WithinSla;
    }
}
