using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Triage;

namespace SupportDesk.UnitTests.TestDoubles;

/// <summary>
/// The SLA policy from the assignment (Critical 4 h, High 8 h, Medium 24 h, Low 72 h, Premium
/// half with a 1 h floor, at risk at 25%). Tests that need other numbers use <see cref="With"/>.
/// </summary>
public static class TestSlaPolicy
{
    public static SlaPolicy Default() => With();

    public static SlaPolicy With(
        double low = 72,
        double medium = 24,
        double high = 8,
        double critical = 4,
        double premiumMultiplier = 0.5,
        double minimumHours = 1,
        double atRiskPercent = 25) => new()
        {
            BaseWindowHours = new Dictionary<TicketPriority, double>
            {
                [TicketPriority.Low] = low,
                [TicketPriority.Medium] = medium,
                [TicketPriority.High] = high,
                [TicketPriority.Critical] = critical
            },
            PremiumCustomerMultiplier = premiumMultiplier,
            MinimumWindowHours = minimumHours,
            AtRiskThresholdPercent = atRiskPercent
        };
}
