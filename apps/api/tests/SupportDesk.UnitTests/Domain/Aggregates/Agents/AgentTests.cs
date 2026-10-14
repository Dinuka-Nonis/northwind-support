using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Agents;

public class AgentTests
{
    private static Agent NewAgent() =>
        new("Alex Turner", "alex.turner@northwind-support.example", 10, FixedClock.DefaultNow);

    [Fact]
    public void NewAgent_HasNoPasswordHash() =>
        Assert.Null(NewAgent().PasswordHash);

    [Fact]
    public void SetPasswordHash_StoresTheHash()
    {
        var agent = NewAgent();

        agent.SetPasswordHash("a-hash");

        Assert.Equal("a-hash", agent.PasswordHash);
    }

    [Fact]
    public void SetPasswordHash_ReplacesAnEarlierHash()
    {
        var agent = NewAgent();
        agent.SetPasswordHash("first");

        agent.SetPasswordHash("second");

        Assert.Equal("second", agent.PasswordHash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SetPasswordHash_RejectsABlankHash(string blank)
    {
        var agent = NewAgent();
        agent.SetPasswordHash("kept");

        Assert.Throws<ArgumentException>(() => agent.SetPasswordHash(blank));
        Assert.Equal("kept", agent.PasswordHash);
    }

    [Fact]
    public void SetPasswordHash_RejectsNull() =>
        Assert.Throws<ArgumentNullException>(() => NewAgent().SetPasswordHash(null!));
}
