using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Triage;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Triage;

public class SlaPolicyTests
{
    private static readonly DateTime Start = FixedClock.DefaultNow;

    [Theory]
    [InlineData(TicketPriority.Critical, CustomerTier.Standard, 4)]
    [InlineData(TicketPriority.High, CustomerTier.Standard, 8)]
    [InlineData(TicketPriority.Medium, CustomerTier.Standard, 24)]
    [InlineData(TicketPriority.Low, CustomerTier.Standard, 72)]
    [InlineData(TicketPriority.Critical, CustomerTier.Premium, 2)]
    [InlineData(TicketPriority.High, CustomerTier.Premium, 4)]
    [InlineData(TicketPriority.Medium, CustomerTier.Premium, 12)]
    [InlineData(TicketPriority.Low, CustomerTier.Premium, 36)]
    public void WindowFor_UsesTheBaseWindowAndPremiumHalving(TicketPriority priority, CustomerTier tier, double expectedHours) =>
        Assert.Equal(TimeSpan.FromHours(expectedHours), TestSlaPolicy.Default().WindowFor(priority, tier));

    [Fact]
    public void WindowFor_PremiumIsNeverShorterThanTheMinimum()
    {
        // 1.5 h halved is 45 minutes, so the 1 hour floor takes over.
        var policy = TestSlaPolicy.With(critical: 1.5);

        Assert.Equal(TimeSpan.FromHours(1), policy.WindowFor(TicketPriority.Critical, CustomerTier.Premium));
    }

    [Fact]
    public void WindowFor_PremiumExactlyAtTheMinimumIsUnchanged()
    {
        var policy = TestSlaPolicy.With(critical: 2);

        Assert.Equal(TimeSpan.FromHours(1), policy.WindowFor(TicketPriority.Critical, CustomerTier.Premium));
    }

    [Fact]
    public void WindowFor_PremiumNeverGetsMoreTimeThanStandard()
    {
        // A base window already under the floor must not be stretched by the floor.
        var policy = TestSlaPolicy.With(critical: 0.5);

        Assert.Equal(TimeSpan.FromHours(0.5), policy.WindowFor(TicketPriority.Critical, CustomerTier.Premium));
    }

    [Fact]
    public void WindowFor_StandardIgnoresTheFloorAndMultiplier() =>
        Assert.Equal(TimeSpan.FromHours(0.5), TestSlaPolicy.With(critical: 0.5).WindowFor(TicketPriority.Critical, CustomerTier.Standard));

    [Fact]
    public void WindowFor_ChangingTheConfigurationChangesTheResult()
    {
        var policy = TestSlaPolicy.With(high: 10, premiumMultiplier: 0.25);

        Assert.Equal(TimeSpan.FromHours(10), policy.WindowFor(TicketPriority.High, CustomerTier.Standard));
        Assert.Equal(TimeSpan.FromHours(2.5), policy.WindowFor(TicketPriority.High, CustomerTier.Premium));
    }

    [Fact]
    public void WindowFor_APriorityWithoutAWindowThrows()
    {
        var policy = TestSlaPolicy.Default();
        policy.BaseWindowHours.Remove(TicketPriority.High);

        var ex = Assert.Throws<InvalidOperationException>(() => policy.WindowFor(TicketPriority.High, CustomerTier.Standard));

        Assert.Contains("High", ex.Message);
    }

    [Fact]
    public void DueAt_AddsTheWindowToTheStartTime()
    {
        var due = TestSlaPolicy.Default().DueAt(TicketPriority.High, CustomerTier.Standard, Start);

        Assert.Equal(Start.AddHours(8), due.DueAtUtc);
        Assert.Equal(TimeSpan.FromHours(8), due.Window);
    }

    [Fact]
    public void DueAt_ForPremiumUsesTheShorterWindow()
    {
        var due = TestSlaPolicy.Default().DueAt(TicketPriority.Critical, CustomerTier.Premium, Start);

        Assert.Equal(Start.AddHours(2), due.DueAtUtc);
    }

    [Fact]
    public void DueAt_ExplainsStandardAndPremiumWindows()
    {
        var policy = TestSlaPolicy.Default();

        Assert.Equal("Medium priority: 24 h window.", policy.DueAt(TicketPriority.Medium, CustomerTier.Standard, Start).Reason);
        Assert.Equal(
            "High priority: 8 h base window x 0.5 for a Premium customer = 4 h.",
            policy.DueAt(TicketPriority.High, CustomerTier.Premium, Start).Reason);
    }

    [Fact]
    public void DueAt_MentionsTheMinimumWhenItApplies()
    {
        var due = TestSlaPolicy.With(critical: 1.5).DueAt(TicketPriority.Critical, CustomerTier.Premium, Start);

        Assert.Contains("minimum window applied", due.Reason);
    }

    [Theory]
    [InlineData(25, 0.25)]
    [InlineData(10, 0.1)]
    public void AtRiskFraction_IsThePercentAsAFraction(double percent, double expected) =>
        Assert.Equal(expected, TestSlaPolicy.With(atRiskPercent: percent).AtRiskFraction, precision: 10);

    [Fact]
    public void EnsureValid_AcceptsTheDefaultPolicy() =>
        TestSlaPolicy.Default().EnsureValid();

    [Fact]
    public void EnsureValid_RejectsAnEmptyPolicy() =>
        Assert.Throws<InvalidOperationException>(() => new SlaPolicy().EnsureValid());

    [Fact]
    public void EnsureValid_RejectsAMissingPriority()
    {
        var policy = TestSlaPolicy.Default();
        policy.BaseWindowHours.Remove(TicketPriority.Low);

        var ex = Assert.Throws<InvalidOperationException>(policy.EnsureValid);

        Assert.Contains("Low", ex.Message);
    }

    [Theory]
    [InlineData(0, 1, 25)]
    [InlineData(1.5, 1, 25)]
    [InlineData(0.5, 0, 25)]
    [InlineData(0.5, 1, 0)]
    [InlineData(0.5, 1, 100)]
    public void EnsureValid_RejectsOutOfRangeNumbers(double multiplier, double minimumHours, double atRiskPercent)
    {
        var policy = TestSlaPolicy.With(premiumMultiplier: multiplier, minimumHours: minimumHours, atRiskPercent: atRiskPercent);

        Assert.Throws<InvalidOperationException>(policy.EnsureValid);
    }
}
