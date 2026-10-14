using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class TicketTests
{
    private static readonly DateTime Later = FixedClock.DefaultNow.AddHours(3);

    [Fact]
    public void Raise_StartsNewUnassignedAndWithoutADueDate()
    {
        var ticket = Ticket.Raise(
            "TCK-0041", "  Cannot log in  ", "  Happens for every user.  ", 1, 3,
            TicketPriority.High, FixedClock.DefaultNow);

        Assert.Equal(TicketStatus.New, ticket.Status);
        Assert.Equal("Cannot log in", ticket.Title);
        Assert.Equal("Happens for every user.", ticket.Description);
        Assert.Null(ticket.AssignedAgentId);
        Assert.Null(ticket.DueAtUtc);
        Assert.Equal(FixedClock.DefaultNow, ticket.CreatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, ticket.UpdatedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenResolving_StampsResolutionAndUpdateTimes()
    {
        var ticket = new TicketBuilder().WithStatus(TicketStatus.InProgress).Build();

        ticket.ChangeStatus(TicketStatus.Resolved, Later);

        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        Assert.Equal(Later, ticket.ResolvedAtUtc);
        Assert.Equal(Later, ticket.UpdatedAtUtc);
        Assert.False(ticket.IsOpen);
    }

    [Fact]
    public void ChangeStatus_WhenReopening_ClearsTheResolutionTime()
    {
        var ticket = new TicketBuilder()
            .WithStatus(TicketStatus.Resolved)
            .ResolvedAt(FixedClock.DefaultNow)
            .Build();

        ticket.ChangeStatus(TicketStatus.InProgress, Later);

        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Null(ticket.ResolvedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenClosingAnUnresolvedTicket_RecordsAResolutionTime()
    {
        var ticket = new TicketBuilder().WithStatus(TicketStatus.Open).Build();

        ticket.ChangeStatus(TicketStatus.Closed, Later);

        Assert.Equal(Later, ticket.ResolvedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenClosingAResolvedTicket_KeepsTheOriginalResolutionTime()
    {
        var ticket = new TicketBuilder()
            .WithStatus(TicketStatus.Resolved)
            .ResolvedAt(FixedClock.DefaultNow)
            .Build();

        ticket.ChangeStatus(TicketStatus.Closed, Later);

        Assert.Equal(FixedClock.DefaultNow, ticket.ResolvedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenTicketIsAlreadyClosed_Throws()
    {
        var ticket = new TicketBuilder()
            .WithReference("TCK-0026")
            .WithStatus(TicketStatus.Closed)
            .Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => ticket.ChangeStatus(TicketStatus.Open, Later));

        Assert.Contains("TCK-0026", exception.Message);
        Assert.Equal(TicketStatus.Closed, ticket.Status);
    }

    [Fact]
    public void AssignTo_WithoutAnAgent_TakesTheTicketBack()
    {
        var ticket = new TicketBuilder().AssignedTo(2).Build();

        ticket.AssignTo(null, Later);

        Assert.Null(ticket.AssignedAgentId);
        Assert.Equal(Later, ticket.UpdatedAtUtc);
    }

    [Fact]
    public void Raise_WithADueDateAndAnAgent_StartsWithBoth()
    {
        var due = FixedClock.DefaultNow.AddHours(4);

        var ticket = Ticket.Raise(
            "TCK-0042", "Cannot log in", "Happens for every user.", 1, 3,
            TicketPriority.Critical, FixedClock.DefaultNow, due, assignedAgentId: 7);

        Assert.Equal(TicketStatus.New, ticket.Status);
        Assert.Equal(due, ticket.DueAtUtc);
        Assert.Equal(7, ticket.AssignedAgentId);
    }

    [Theory]
    [InlineData(TicketPriority.Low, TicketPriority.Medium)]
    [InlineData(TicketPriority.Medium, TicketPriority.High)]
    [InlineData(TicketPriority.High, TicketPriority.Critical)]
    public void PriorityAfterEscalation_IsOneLevelHigher(TicketPriority current, TicketPriority expected)
    {
        var ticket = new TicketBuilder().WithPriority(current).Build();

        Assert.Equal(expected, ticket.PriorityAfterEscalation());
    }

    [Fact]
    public void PriorityAfterEscalation_WhenCritical_Throws()
    {
        var ticket = new TicketBuilder().WithReference("TCK-0010").WithPriority(TicketPriority.Critical).Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => ticket.PriorityAfterEscalation());

        Assert.Contains("TCK-0010", exception.Message);
        Assert.Contains("already Critical", exception.Message);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void PriorityAfterEscalation_WhenResolvedOrClosed_Throws(TicketStatus status)
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Low).WithStatus(status).Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => ticket.PriorityAfterEscalation());

        Assert.Contains(status.ToString(), exception.Message);
    }

    [Fact]
    public void PriorityAfterEscalation_WhenResolvedAndCritical_NamesTheStatusNotThePriority()
    {
        var ticket = new TicketBuilder()
            .WithPriority(TicketPriority.Critical)
            .WithStatus(TicketStatus.Resolved)
            .Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => ticket.PriorityAfterEscalation());

        Assert.Contains("Resolved", exception.Message);
        Assert.DoesNotContain("already Critical", exception.Message);
    }

    [Fact]
    public void Escalate_RaisesThePriorityAndAppliesTheNewDueDateAndAgent()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Medium).AssignedTo(2).Build();
        var newDue = Later.AddHours(8);

        ticket.Escalate("Customer is blocked", "lead.sam", newDue, 3, Later);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(newDue, ticket.DueAtUtc);
        Assert.Equal(3, ticket.AssignedAgentId);
        Assert.Equal(Later, ticket.UpdatedAtUtc);
    }

    [Fact]
    public void Escalate_ReturnsAHistoryRowWithTheBeforeAndAfterValues()
    {
        var oldDue = FixedClock.DefaultNow.AddHours(24);
        var newDue = Later.AddHours(8);
        var ticket = new TicketBuilder()
            .WithPriority(TicketPriority.Medium)
            .AssignedTo(2)
            .DueAt(oldDue)
            .Build();

        var row = ticket.Escalate("Customer is blocked", "lead.sam", newDue, 3, Later);

        Assert.Equal(ticket.Id, row.TicketId);
        Assert.Equal(TicketPriority.Medium, row.FromPriority);
        Assert.Equal(TicketPriority.High, row.ToPriority);
        Assert.Equal(oldDue, row.FromDueAtUtc);
        Assert.Equal(newDue, row.ToDueAtUtc);
        Assert.Equal(2, row.FromAgentId);
        Assert.Equal(3, row.ToAgentId);
        Assert.Equal("Customer is blocked", row.Reason);
        Assert.Equal("lead.sam", row.EscalatedBy);
        Assert.Equal(Later, row.EscalatedAtUtc);
    }

    [Fact]
    public void Escalate_AnUnassignedTicketWithoutADueDate_RecordsNullsForTheOldValues()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Low).Build();

        var row = ticket.Escalate("Needs attention soon", "lead.sam", Later.AddHours(24), null, Later);

        Assert.Null(row.FromAgentId);
        Assert.Null(row.ToAgentId);
        Assert.Null(row.FromDueAtUtc);
        Assert.Null(ticket.AssignedAgentId);
    }

    [Fact]
    public void Escalate_KeepingTheSameAgent_RecordsThemOnBothSides()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.High).AssignedTo(4).Build();

        var row = ticket.Escalate("Still not fixed", "lead.sam", Later.AddHours(4), 4, Later);

        Assert.Equal(4, row.FromAgentId);
        Assert.Equal(4, row.ToAgentId);
    }

    [Fact]
    public void Escalate_TrimsTheReasonAndTheActor()
    {
        var ticket = new TicketBuilder().Build();

        var row = ticket.Escalate("  Customer is blocked  ", "  lead.sam  ", Later.AddHours(8), null, Later);

        Assert.Equal("Customer is blocked", row.Reason);
        Assert.Equal("lead.sam", row.EscalatedBy);
    }

    [Fact]
    public void Escalate_CanBeRepeatedUntilTheTicketIsCritical()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Low).Build();

        ticket.Escalate("First escalation", "lead.sam", Later.AddHours(24), null, Later);
        ticket.Escalate("Second escalation", "lead.sam", Later.AddHours(8), null, Later);
        ticket.Escalate("Third escalation", "lead.sam", Later.AddHours(4), null, Later);

        Assert.Equal(TicketPriority.Critical, ticket.Priority);
        Assert.Throws<BusinessRuleViolationException>(
            () => ticket.Escalate("Fourth escalation", "lead.sam", Later.AddHours(4), null, Later));
    }

    [Fact]
    public void Escalate_WhenCritical_ChangesNothing()
    {
        var due = FixedClock.DefaultNow.AddHours(4);
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Critical).AssignedTo(2).DueAt(due).Build();

        Assert.Throws<BusinessRuleViolationException>(
            () => ticket.Escalate("Please look again", "lead.sam", Later.AddHours(4), 3, Later));

        Assert.Equal(TicketPriority.Critical, ticket.Priority);
        Assert.Equal(due, ticket.DueAtUtc);
        Assert.Equal(2, ticket.AssignedAgentId);
        Assert.Equal(FixedClock.DefaultNow, ticket.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void Escalate_WhenResolvedOrClosed_ChangesNothing(TicketStatus status)
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Low).WithStatus(status).Build();

        Assert.Throws<BusinessRuleViolationException>(
            () => ticket.Escalate("Please look again", "lead.sam", Later.AddHours(8), null, Later));

        Assert.Equal(TicketPriority.Low, ticket.Priority);
    }

    [Theory]
    [InlineData("")]
    [InlineData("     ")]
    [InlineData("abcd")]
    [InlineData("  abcd  ")]
    public void Escalate_WithATooShortReason_ThrowsAndChangesNothing(string reason)
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Low).Build();

        Assert.Throws<ArgumentException>(() => ticket.Escalate(reason, "lead.sam", Later.AddHours(8), null, Later));

        Assert.Equal(TicketPriority.Low, ticket.Priority);
        Assert.Null(ticket.DueAtUtc);
    }

    [Fact]
    public void Escalate_ReasonLengthBoundariesAre5And500()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Low).Build();

        ticket.Escalate(new string('a', 5), "lead.sam", Later.AddHours(8), null, Later);
        ticket.Escalate(new string('a', 500), "lead.sam", Later.AddHours(4), null, Later);

        Assert.Throws<ArgumentException>(
            () => new TicketBuilder().Build().Escalate(new string('a', 501), "lead.sam", Later, null, Later));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Escalate_WithABlankActor_Throws(string actor)
    {
        var ticket = new TicketBuilder().Build();

        Assert.Throws<ArgumentException>(() => ticket.Escalate("Customer is blocked", actor, Later.AddHours(8), null, Later));
    }

    [Fact]
    public void Escalate_WithAnActorOver100Characters_Throws()
    {
        var ticket = new TicketBuilder().Build();

        Assert.Throws<ArgumentException>(
            () => ticket.Escalate("Customer is blocked", new string('a', 101), Later.AddHours(8), null, Later));
    }
}
