using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Triage;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketCommandHandlerTests
{
    // Tickets built by TicketBuilder are in category 1, so specialists list 1.
    private const int TicketCategory = 1;

    // The signed-in agent's name, as the controller reads it from the token. Padded on purpose,
    // to show that the history row stores it trimmed.
    private const string Actor = "  Priya Nair  ";

    private static EscalateTicketRequest Request() => new("  Customer is escalating again  ");

    private static AgentCandidate Candidate(
        int id,
        int open,
        int max = 5,
        bool active = true,
        int[]? categories = null) =>
        new(id, $"Agent {id}", active, max, open, categories ?? []);

    [Fact]
    public async Task HandleAsync_RaisesThePriorityOneLevel_AndRecordsTheEscalation()
    {
        var context = new TicketCommandTestContext();
        var oldDue = FixedClock.DefaultNow.AddHours(24);
        var ticket = context.ExistingTicket(
            new TicketBuilder().WithPriority(TicketPriority.Medium).DueAt(oldDue).AssignedTo(2).Build());
        context.WithCandidates(Candidate(2, open: 1));

        context.Clock.Advance(TimeSpan.FromHours(3));

        var result = await context.EscalateTicket.HandleAsync(ticket.Id, Request(), Actor, CancellationToken.None);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(context.Clock.UtcNow, ticket.UpdatedAtUtc);

        var row = context.AddedEscalation;
        Assert.NotNull(row);
        Assert.Equal(TicketPriority.Medium, row.FromPriority);
        Assert.Equal(TicketPriority.High, row.ToPriority);
        Assert.Equal(oldDue, row.FromDueAtUtc);
        Assert.Equal(ticket.DueAtUtc, row.ToDueAtUtc);
        Assert.Equal("Customer is escalating again", row.Reason);
        Assert.Equal("Priya Nair", row.EscalatedBy);
        Assert.Equal(context.Clock.UtcNow, row.EscalatedAtUtc);

        // The row is staged and saved together with the ticket, once.
        context.Tickets.Verify(
            t => t.AddEscalationAsync(It.IsAny<TicketEscalation>(), It.IsAny<CancellationToken>()), Times.Once);
        context.VerifySaved(Times.Once());

        // The response carries the ticket and the row as they read back.
        Assert.Equal(41, result.Ticket.Id);
        Assert.Same(context.RecordedEscalation, result.Escalation);
        Assert.False(string.IsNullOrWhiteSpace(result.SlaReason));
    }

    [Fact]
    public async Task HandleAsync_RecordsTheEscalationUnderTheNameItWasGiven()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(TicketPriority.Medium).Build());

        // The name is a parameter, not part of the request, so whoever is signed in is who is recorded.
        await context.EscalateTicket.HandleAsync(
            ticket.Id, Request(), "Sara Lindqvist", CancellationToken.None);

        Assert.NotNull(context.AddedEscalation);
        Assert.Equal("Sara Lindqvist", context.AddedEscalation.EscalatedBy);
    }

    [Theory]
    [InlineData(CustomerTier.Standard, 8)]
    [InlineData(CustomerTier.Premium, 4)]
    public async Task HandleAsync_CountsTheNewDueDateFromNow_NotFromCreation(CustomerTier tier, double expectedHours)
    {
        var context = new TicketCommandTestContext();
        context.ExistingCustomer(tier);
        var ticket = context.ExistingTicket(
            new TicketBuilder().WithPriority(TicketPriority.Medium).DueAt(FixedClock.DefaultNow.AddHours(24)).Build());

        context.Clock.Advance(TimeSpan.FromHours(10));

        await context.EscalateTicket.HandleAsync(ticket.Id, Request(), Actor, CancellationToken.None);

        // High is 8 h (4 h for Premium), counted from the moment of escalation.
        Assert.Equal(context.Clock.UtcNow.AddHours(expectedHours), ticket.DueAtUtc);
    }

    [Fact]
    public async Task HandleAsync_KeepsTheCurrentAgent_EvenWhenAnotherHasFewerTicketsAndTheyAreFull()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(TicketPriority.Medium).AssignedTo(2).Build());

        // Agent 2 is at their limit and agent 1 is idle, but the ticket already counts towards
        // agent 2's load, so escalating it must not take it away from them.
        context.WithCandidates(Candidate(1, open: 0), Candidate(2, open: 5, max: 5));

        var result = await context.EscalateTicket.HandleAsync(ticket.Id, Request(), Actor, CancellationToken.None);

        Assert.Equal(2, ticket.AssignedAgentId);
        Assert.NotNull(context.AddedEscalation);
        Assert.Equal(2, context.AddedEscalation.FromAgentId);
        Assert.Equal(2, context.AddedEscalation.ToAgentId);
        Assert.Contains("Kept", result.AssignmentReason);
    }

    [Fact]
    public async Task HandleAsync_ReassignsPerTheAssignmentRules_WhenTheCurrentAgentIsInactive()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(TicketPriority.High).AssignedTo(2).Build());
        context.WithCandidates(
            Candidate(2, open: 1, active: false),
            Candidate(3, open: 2),
            Candidate(4, open: 3));

        var result = await context.EscalateTicket.HandleAsync(ticket.Id, Request(), Actor, CancellationToken.None);

        Assert.Equal(3, ticket.AssignedAgentId);
        Assert.NotNull(context.AddedEscalation);
        Assert.Equal(2, context.AddedEscalation.FromAgentId);
        Assert.Equal(3, context.AddedEscalation.ToAgentId);
        Assert.Contains("previous agent", result.AssignmentReason);
    }

    [Fact]
    public async Task HandleAsync_ReassignsToASpecialist_WhenTheCurrentAgentIsNotQualified()
    {
        var context = new TicketCommandTestContext();
        context.ExistingCategory(requiresSpecialist: true);
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(TicketPriority.Low).AssignedTo(2).Build());
        context.WithCandidates(
            Candidate(2, open: 0),
            Candidate(3, open: 4, categories: [TicketCategory]));

        await context.EscalateTicket.HandleAsync(ticket.Id, Request(), Actor, CancellationToken.None);

        Assert.Equal(3, ticket.AssignedAgentId);
    }

    [Fact]
    public async Task HandleAsync_GivesAnUnassignedTicketAnOwner()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(TicketPriority.Low).Build());
        context.WithCandidates(Candidate(5, open: 0));

        await context.EscalateTicket.HandleAsync(ticket.Id, Request(), Actor, CancellationToken.None);

        Assert.Equal(5, ticket.AssignedAgentId);
        Assert.NotNull(context.AddedEscalation);
        Assert.Null(context.AddedEscalation.FromAgentId);
        Assert.Equal(5, context.AddedEscalation.ToAgentId);
    }

    [Fact]
    public async Task HandleAsync_WhenNobodyIsEligible_StillEscalates_AndLeavesTheTicketUnassigned()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(TicketPriority.Medium).AssignedTo(2).Build());
        context.WithCandidates(Candidate(2, open: 1, active: false), Candidate(3, open: 5, max: 5));

        var result = await context.EscalateTicket.HandleAsync(ticket.Id, Request(), Actor, CancellationToken.None);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Null(ticket.AssignedAgentId);
        Assert.NotNull(context.AddedEscalation);
        Assert.Equal(2, context.AddedEscalation.FromAgentId);
        Assert.Null(context.AddedEscalation.ToAgentId);
        Assert.Contains("unassigned", result.AssignmentReason);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WhenTheTicketDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();
        context.Tickets
            .Setup(t => t.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ticket?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.EscalateTicket.HandleAsync(99, Request(), Actor, CancellationToken.None));

        Assert.Contains("Ticket", exception.Message);
        context.VerifySaved(Times.Never());
    }

    [Theory]
    [InlineData(TicketPriority.Critical, TicketStatus.Open, "already Critical")]
    [InlineData(TicketPriority.Medium, TicketStatus.Resolved, "Resolved")]
    [InlineData(TicketPriority.Low, TicketStatus.Closed, "Closed")]
    [InlineData(TicketPriority.Critical, TicketStatus.Resolved, "Resolved")]
    public async Task HandleAsync_WhenTheTicketCannotBeEscalated_Throws_NamesTheReason_AndSavesNothing(
        TicketPriority priority,
        TicketStatus status,
        string expectedInMessage)
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(priority).WithStatus(status).Build());
        var updatedBefore = ticket.UpdatedAtUtc;

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, Request(), Actor, CancellationToken.None));

        Assert.Contains(expectedInMessage, exception.Message);
        Assert.Equal(priority, ticket.Priority);
        Assert.Equal(updatedBefore, ticket.UpdatedAtUtc);
        Assert.Null(context.AddedEscalation);
        context.VerifySaved(Times.Never());
    }
}
