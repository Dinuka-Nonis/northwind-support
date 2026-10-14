using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Triage;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class RaiseTicketCommandHandlerTests
{
    // The request is always for category 3, so agents who specialise in it list 3.
    private const int RequestedCategory = 3;

    private static CreateTicketRequest Request(TicketPriority? priority = null) =>
        new("Cannot log in to the portal", "Happens for every user since this morning.", 1, RequestedCategory, priority);

    private static AgentCandidate Candidate(
        int id,
        int open,
        int max = 5,
        bool active = true,
        int[]? categories = null) =>
        new(id, $"Agent {id}", active, max, open, categories ?? []);

    [Fact]
    public async Task HandleAsync_StampsReferenceStatusAndTimestamps()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(TicketPriority.High), CancellationToken.None);

        var created = context.AddedTicket;
        Assert.NotNull(created);
        Assert.Equal("TCK-0041", created.Reference);
        Assert.Equal(TicketStatus.New, created.Status);
        Assert.Equal(TicketPriority.High, created.Priority);
        Assert.Equal(FixedClock.DefaultNow, created.CreatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, created.UpdatedAtUtc);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WithoutRequestedPriority_FallsBackToMedium()
    {
        var context = new TicketCommandTestContext();

        var result = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Medium, context.AddedTicket.Priority);
        Assert.NotNull(result.Triage);
        Assert.Equal(TicketPriority.Medium, result.Triage.Priority);
        Assert.Contains("default", result.Triage.PriorityReason);
    }

    [Fact]
    public async Task HandleAsync_ForACategoryThatForcesCritical_IgnoresTheRequestedPriority()
    {
        var context = new TicketCommandTestContext();
        context.ExistingCategory(forcesCritical: true);

        var result = await context.RaiseTicket.HandleAsync(Request(TicketPriority.Low), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Critical, context.AddedTicket.Priority);
        Assert.NotNull(result.Triage);
        Assert.Equal(TicketPriority.Critical, result.Triage.Priority);

        // The reason says what was asked for, so the person can see it was overruled.
        Assert.Contains("Low", result.Triage.PriorityReason);
    }

    [Theory]
    [InlineData(TicketPriority.Critical, CustomerTier.Standard, 4)]
    [InlineData(TicketPriority.High, CustomerTier.Standard, 8)]
    [InlineData(TicketPriority.Medium, CustomerTier.Standard, 24)]
    [InlineData(TicketPriority.Low, CustomerTier.Standard, 72)]
    [InlineData(TicketPriority.High, CustomerTier.Premium, 4)]
    [InlineData(TicketPriority.Low, CustomerTier.Premium, 36)]
    public async Task HandleAsync_DueDateIsNowPlusTheWindow_HalvedForPremium(
        TicketPriority priority,
        CustomerTier tier,
        double expectedHours)
    {
        var context = new TicketCommandTestContext();
        context.ExistingCustomer(tier);

        var result = await context.RaiseTicket.HandleAsync(Request(priority), CancellationToken.None);

        var expectedDue = FixedClock.DefaultNow.AddHours(expectedHours);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(expectedDue, context.AddedTicket.DueAtUtc);
        Assert.NotNull(result.Triage);
        Assert.Equal(expectedDue, result.Triage.DueAtUtc);
        Assert.Equal(expectedHours, result.Triage.SlaWindowHours);
        Assert.False(string.IsNullOrWhiteSpace(result.Triage.SlaReason));
    }

    [Fact]
    public async Task HandleAsync_GivesTheTicketToTheLeastLoadedSpecialist()
    {
        var context = new TicketCommandTestContext();
        context.ExistingCategory(requiresSpecialist: true);
        context.WithCandidates(
            Candidate(1, open: 0),                                        // not a specialist
            Candidate(2, open: 4, categories: [RequestedCategory]),
            Candidate(3, open: 1, categories: [RequestedCategory]),       // the one to pick
            Candidate(4, open: 0, categories: [RequestedCategory], active: false));

        var result = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(3, context.AddedTicket.AssignedAgentId);
        Assert.NotNull(result.Triage);
        Assert.NotNull(result.Triage.AssignedAgent);
        Assert.Equal(3, result.Triage.AssignedAgent.Id);
        Assert.Contains("Agent 3", result.Triage.AssignmentReason);
    }

    [Fact]
    public async Task HandleAsync_WhenNobodyIsEligible_StillRaisesTheTicket_Unassigned_WithTheReason()
    {
        var context = new TicketCommandTestContext();
        context.WithCandidates(Candidate(1, open: 5, max: 5), Candidate(2, open: 0, active: false));

        var result = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Null(context.AddedTicket.AssignedAgentId);
        Assert.NotNull(result.Triage);
        Assert.Null(result.Triage.AssignedAgent);
        Assert.Contains("unassigned", result.Triage.AssignmentReason);

        // The due date still applies: only the owner is missing.
        Assert.NotNull(context.AddedTicket.DueAtUtc);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WhenCustomerDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();
        context.Customers
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(Request(), CancellationToken.None));

        Assert.Contains("Customer", exception.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenCategoryDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();
        context.Categories
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(Request(), CancellationToken.None));

        Assert.Contains("Category", exception.Message);
        Assert.Null(context.AddedTicket);
        context.VerifySaved(Times.Never());
    }
}
