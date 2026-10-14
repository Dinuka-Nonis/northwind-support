using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketRequestValidatorTests
{
    private readonly EscalateTicketRequestValidator _validator = new();

    private static EscalateTicketRequest Request(string? reason = "A good enough reason") => new(reason!);

    private bool IsValid(EscalateTicketRequest request) => _validator.Validate(request).IsValid;

    private IEnumerable<string> FailedFields(EscalateTicketRequest request) =>
        _validator.Validate(request).Errors.Select(failure => failure.PropertyName).Distinct();

    [Fact]
    public void AValidRequest_Passes() => Assert.True(IsValid(Request()));

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void TheReason_MustBeFiveToFiveHundredCharacters(int length, bool expected) =>
        Assert.Equal(expected, IsValid(Request(reason: new string('r', length))));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("     ")]
    public void ABlankReason_IsRejected(string? reason)
    {
        var request = Request(reason: reason);

        Assert.False(IsValid(request));
        Assert.Contains("Reason", FailedFields(request));
    }

    [Fact]
    public void TheReason_IsMeasuredAfterTrimming()
    {
        // Four real characters inside padding is still too short, and padding around a
        // 500-character reason does not push it over the limit.
        Assert.False(IsValid(Request(reason: "   abcd   ")));
        Assert.True(IsValid(Request(reason: "  " + new string('r', 500) + "  ")));
    }

    [Fact]
    public void ATooShortReason_ReportsOnlyTheReason()
    {
        // Who escalated is not part of the request any more, so the reason is the only field there is to fail.
        var failed = FailedFields(Request(reason: "no"));

        Assert.Equal(new[] { "Reason" }, failed);
    }
}
