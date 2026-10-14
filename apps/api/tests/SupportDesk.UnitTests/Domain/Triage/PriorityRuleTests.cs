using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Triage;

namespace SupportDesk.UnitTests.Domain.Triage;

public class PriorityRuleTests
{
    [Fact]
    public void Decide_WithNoRequestedPriority_DefaultsToMedium()
    {
        var decision = PriorityRule.Decide(null, categoryForcesCritical: false);

        Assert.Equal(TicketPriority.Medium, decision.Priority);
        Assert.Contains("default", decision.Reason);
    }

    [Theory]
    [InlineData(TicketPriority.Low)]
    [InlineData(TicketPriority.Medium)]
    [InlineData(TicketPriority.High)]
    [InlineData(TicketPriority.Critical)]
    public void Decide_UsesTheRequestedPriority(TicketPriority requested)
    {
        var decision = PriorityRule.Decide(requested, categoryForcesCritical: false);

        Assert.Equal(requested, decision.Priority);
        Assert.Contains("requested", decision.Reason);
    }

    [Theory]
    [InlineData(TicketPriority.Low)]
    [InlineData(TicketPriority.Medium)]
    [InlineData(TicketPriority.High)]
    [InlineData(TicketPriority.Critical)]
    public void Decide_ForcedCategoryAlwaysGivesCritical(TicketPriority requested)
    {
        var decision = PriorityRule.Decide(requested, categoryForcesCritical: true);

        Assert.Equal(TicketPriority.Critical, decision.Priority);
    }

    [Fact]
    public void Decide_ForcedCategoryWithNoRequestStillGivesCritical()
    {
        var decision = PriorityRule.Decide(null, categoryForcesCritical: true);

        Assert.Equal(TicketPriority.Critical, decision.Priority);
        Assert.Contains("none was requested", decision.Reason);
    }

    [Fact]
    public void Decide_ForcedCategoryReasonMentionsWhatWasRequested()
    {
        var decision = PriorityRule.Decide(TicketPriority.Low, categoryForcesCritical: true);

        Assert.Contains("Low was requested", decision.Reason);
    }
}
