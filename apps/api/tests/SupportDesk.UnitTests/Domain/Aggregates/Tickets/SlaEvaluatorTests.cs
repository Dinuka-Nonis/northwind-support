using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaEvaluatorTests
{
    private const double AtRisk25Percent = 0.25;

    private static readonly DateTime Now = FixedClock.DefaultNow;

    // A 4 hour window that started now, so 25% of it is exactly 1 hour.
    private static readonly DateTime Created = Now;
    private static readonly DateTime Due = Now.AddHours(4);

    private static SlaStatus Open(DateTime now) =>
        SlaEvaluator.Evaluate(Created, Due, null, now, AtRisk25Percent);

    private static SlaStatus Resolved(DateTime resolvedAt) =>
        SlaEvaluator.Evaluate(Created, Due, resolvedAt, Due.AddDays(1), AtRisk25Percent);

    [Fact]
    public void Evaluate_WithoutADueDate_IsNotApplicable() =>
        Assert.Equal(SlaStatus.NotApplicable, SlaEvaluator.Evaluate(Created, null, null, Now, AtRisk25Percent));

    [Fact]
    public void Evaluate_WithoutADueDate_IsNotApplicableEvenWhenResolved() =>
        Assert.Equal(SlaStatus.NotApplicable, SlaEvaluator.Evaluate(Created, null, Now, Now, AtRisk25Percent));

    [Fact]
    public void Evaluate_ResolvedBeforeDue_IsMet() =>
        Assert.Equal(SlaStatus.Met, Resolved(Due.AddMinutes(-1)));

    [Fact]
    public void Evaluate_ResolvedExactlyOnTheDueDate_IsMet() =>
        Assert.Equal(SlaStatus.Met, Resolved(Due));

    [Fact]
    public void Evaluate_ResolvedOneMillisecondLate_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, Resolved(Due.AddMilliseconds(1)));

    [Fact]
    public void Evaluate_ResolvedInsideTheAtRiskZone_IsStillMet() =>
        Assert.Equal(SlaStatus.Met, Resolved(Due.AddMinutes(-10)));

    [Fact]
    public void Evaluate_OpenAndFarFromDue_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, Open(Now.AddMinutes(1)));

    [Fact]
    public void Evaluate_OpenWithOneMinuteMoreThanThresholdLeft_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, Open(Due.AddMinutes(-61)));

    [Fact]
    public void Evaluate_OpenWithExactlyTheThresholdLeft_IsAtRisk() =>
        Assert.Equal(SlaStatus.AtRisk, Open(Due.AddHours(-1)));

    [Fact]
    public void Evaluate_OpenWithLessThanTheThresholdLeft_IsAtRisk() =>
        Assert.Equal(SlaStatus.AtRisk, Open(Due.AddMinutes(-1)));

    [Fact]
    public void Evaluate_OpenAtExactlyTheDueDate_IsAtRisk() =>
        Assert.Equal(SlaStatus.AtRisk, Open(Due));

    [Fact]
    public void Evaluate_OpenOneMillisecondPastDue_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, Open(Due.AddMilliseconds(1)));

    [Fact]
    public void Evaluate_OpenAndLongPastDue_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, Open(Due.AddDays(3)));

    [Fact]
    public void Evaluate_UsesTheGivenThreshold()
    {
        // 3 hours left of 4 is 75%: within SLA at 25%, at risk once the threshold is 75%.
        var now = Due.AddHours(-3);

        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(Created, Due, null, now, 0.25));
        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(Created, Due, null, now, 0.75));
    }

    [Fact]
    public void Evaluate_MeasuresTheWindowFromCreation()
    {
        // 2 hours left: that is 50% of a 4 h window but only 20% of a 10 h window.
        var now = Due.AddHours(-2);
        var longWindowCreated = Due.AddHours(-10);

        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(Created, Due, null, now, AtRisk25Percent));
        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(longWindowCreated, Due, null, now, AtRisk25Percent));
    }
}
