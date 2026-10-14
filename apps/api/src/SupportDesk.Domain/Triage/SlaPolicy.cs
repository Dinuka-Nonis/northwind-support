using System.Globalization;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Triage;

/// <summary>
/// The SLA rules as numbers: how long each priority is given, what Premium customers get,
/// the shortest window allowed, and when an open ticket counts as at risk.
/// </summary>
/// <remarks>
/// Property names match the <c>Sla</c> section of the configuration so it binds directly.
/// Nothing here has a default on purpose: the numbers come from configuration only, so a
/// missing value fails loudly instead of quietly using a hidden number. No category names
/// appear in this class - category rules live on the category flags.
/// </remarks>
public sealed class SlaPolicy
{
    /// <summary>Base window in hours for every priority.</summary>
    public Dictionary<TicketPriority, double> BaseWindowHours { get; set; } = new();

    /// <summary>Multiplier applied to the window of a Premium customer (0.5 = half).</summary>
    public double PremiumCustomerMultiplier { get; set; }

    /// <summary>A Premium window is never shorter than this many hours.</summary>
    public double MinimumWindowHours { get; set; }

    /// <summary>An open ticket is at risk once this percent of its window (or less) is left.</summary>
    public double AtRiskThresholdPercent { get; set; }

    /// <summary>The at-risk threshold as a fraction of the window (25 becomes 0.25).</summary>
    public double AtRiskFraction => AtRiskThresholdPercent / 100d;

    /// <summary>
    /// How long a ticket of this priority is given. Premium customers get the base window times
    /// the multiplier, but never less than the minimum - and never more than a Standard customer.
    /// </summary>
    public TimeSpan WindowFor(TicketPriority priority, CustomerTier tier) =>
        TimeSpan.FromHours(HoursFor(priority, tier).Hours);

    /// <summary>The due date for a ticket starting at <paramref name="fromUtc"/>, with the reason.</summary>
    public SlaDueDate DueAt(TicketPriority priority, CustomerTier tier, DateTime fromUtc)
    {
        var (baseHours, hours, floorApplied) = HoursFor(priority, tier);
        var window = TimeSpan.FromHours(hours);

        var reason = tier == CustomerTier.Premium
            ? PremiumReason(priority, baseHours, hours, floorApplied)
            : $"{priority} priority: {Format(baseHours)} window.";

        return new SlaDueDate(fromUtc + window, window, reason);
    }

    /// <summary>Throws if the configured numbers cannot produce a sensible SLA. Run at startup.</summary>
    public void EnsureValid()
    {
        var problems = new List<string>();

        foreach (var priority in Enum.GetValues<TicketPriority>())
        {
            if (!BaseWindowHours.TryGetValue(priority, out var hours) || hours <= 0)
            {
                problems.Add($"BaseWindowHours:{priority} must be greater than 0.");
            }
        }

        if (PremiumCustomerMultiplier is <= 0 or > 1)
        {
            problems.Add("PremiumCustomerMultiplier must be greater than 0 and at most 1.");
        }

        if (MinimumWindowHours <= 0)
        {
            problems.Add("MinimumWindowHours must be greater than 0.");
        }

        if (AtRiskThresholdPercent is <= 0 or >= 100)
        {
            problems.Add("AtRiskThresholdPercent must be between 0 and 100 (exclusive).");
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException("Invalid 'Sla' configuration: " + string.Join(" ", problems));
        }
    }

    private (double BaseHours, double Hours, bool FloorApplied) HoursFor(TicketPriority priority, CustomerTier tier)
    {
        if (!BaseWindowHours.TryGetValue(priority, out var baseHours))
        {
            throw new InvalidOperationException($"No base SLA window is configured for {priority} priority.");
        }

        if (tier != CustomerTier.Premium)
        {
            return (baseHours, baseHours, false);
        }

        var reduced = baseHours * PremiumCustomerMultiplier;

        // The floor protects against windows that are too tight, but Premium must never end up
        // with more time than Standard, so the base window is the upper limit.
        var hours = Math.Min(baseHours, Math.Max(reduced, MinimumWindowHours));
        return (baseHours, hours, hours > reduced);
    }

    private string PremiumReason(TicketPriority priority, double baseHours, double hours, bool floorApplied)
    {
        var multiplier = PremiumCustomerMultiplier.ToString("0.##", CultureInfo.InvariantCulture);
        var note = floorApplied ? " (minimum window applied)" : string.Empty;

        return $"{priority} priority: {Format(baseHours)} base window x {multiplier} for a Premium customer = {Format(hours)}{note}.";
    }

    private static string Format(double hours) => hours.ToString("0.##", CultureInfo.InvariantCulture) + " h";
}

/// <summary>A computed due date, the window it came from, and a plain-language reason.</summary>
public sealed record SlaDueDate(DateTime DueAtUtc, TimeSpan Window, string Reason);
