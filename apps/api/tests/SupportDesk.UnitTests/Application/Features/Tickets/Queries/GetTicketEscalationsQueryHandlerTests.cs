using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Queries;

public class GetTicketEscalationsQueryHandlerTests
{
    private readonly Mock<ITicketQueries> _queries = new();

    [Fact]
    public async Task HandleAsync_ReturnsTheHistoryAsTheQueryGaveIt()
    {
        var row = new TicketEscalationDto(
            1, TicketPriority.Low, TicketPriority.Medium, null, FixedClock.DefaultNow, null, null,
            "Customer is chasing.", "Priya Nair", FixedClock.DefaultNow);

        _queries.Setup(q => q.GetEscalationsAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<TicketEscalationDto>?)new[] { row });

        var history = await new GetTicketEscalationsQueryHandler(_queries.Object).HandleAsync(7, CancellationToken.None);

        Assert.Same(row, Assert.Single(history));
    }

    [Fact]
    public async Task HandleAsync_ForATicketThatWasNeverEscalated_ReturnsAnEmptyList()
    {
        _queries.Setup(q => q.GetEscalationsAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<TicketEscalationDto>?)Array.Empty<TicketEscalationDto>());

        var history = await new GetTicketEscalationsQueryHandler(_queries.Object).HandleAsync(7, CancellationToken.None);

        Assert.Empty(history);
    }

    [Fact]
    public async Task HandleAsync_ForATicketThatDoesNotExist_Throws()
    {
        _queries.Setup(q => q.GetEscalationsAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<TicketEscalationDto>?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => new GetTicketEscalationsQueryHandler(_queries.Object).HandleAsync(99, CancellationToken.None));
    }
}
