using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketRequestValidatorTests
{
    private readonly EscalateTicketRequestValidator _validator = new();

    private static EscalateTicketRequest Request(string? reason = "A good enough reason", string? escalatedBy = "Priya Nair") =>
        new(reason!, escalatedBy!);

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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AMissingActor_IsRejected(string? escalatedBy)
    {
        var request = Request(escalatedBy: escalatedBy);

        Assert.False(IsValid(request));
        Assert.Contains("EscalatedBy", FailedFields(request));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void TheActor_MustBeOneToOneHundredCharacters(int length, bool expected) =>
        Assert.Equal(expected, IsValid(Request(escalatedBy: new string('a', length))));

    [Fact]
    public void BothFieldsFailing_ReportsBoth()
    {
        var failed = FailedFields(Request(reason: "no", escalatedBy: ""));

        Assert.Equal(new[] { "EscalatedBy", "Reason" }, failed.Order());
    }
}
