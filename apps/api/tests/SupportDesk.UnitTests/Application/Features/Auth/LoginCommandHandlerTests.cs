using Microsoft.Extensions.Logging.Abstractions;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Infrastructure.Services;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Auth;

/// <summary>
/// The sign-in rules, with the real BCrypt hasher so that "the password matches" means what it
/// means in production. The lookup and the token issuer are mocked.
/// </summary>
public class LoginCommandHandlerTests
{
    private const string Password = "Local_Dev_Agent1";
    private const string Email = "alex.turner@northwind-support.example";

    // Hashing is deliberately slow, so it is done once for the whole class.
    private static readonly string PasswordHash = new BCryptPasswordHasher().Hash(Password);

    private static readonly DateTime Expiry = FixedClock.DefaultNow.AddMinutes(60);

    private readonly Mock<IAgentQueries> _agents = new();
    private readonly Mock<ITokenIssuer> _tokens = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _tokens.Setup(t => t.Issue(It.IsAny<CurrentUserDto>()))
            .Returns(new IssuedToken("signed.jwt.value", Expiry));

        _handler = new LoginCommandHandler(
            _agents.Object, new BCryptPasswordHasher(), _tokens.Object, NullLogger<LoginCommandHandler>.Instance);
    }

    [Fact]
    public async Task ARightEmailAndPassword_ReturnsTheTokenItsExpiryAndTheAgent()
    {
        FindAgent(Agent());

        var response = await _handler.HandleAsync(new LoginRequest(Email, Password), CancellationToken.None);

        Assert.Equal("signed.jwt.value", response.Token);
        Assert.Equal(Expiry, response.ExpiresAtUtc);
        Assert.Equal(new CurrentUserDto(1, "Alex Turner", Email), response.User);
    }

    [Fact]
    public async Task TheTokenIsIssuedForTheAgentWhoSignedIn()
    {
        FindAgent(Agent());

        await _handler.HandleAsync(new LoginRequest(Email, Password), CancellationToken.None);

        _tokens.Verify(t => t.Issue(new CurrentUserDto(1, "Alex Turner", Email)), Times.Once);
    }

    [Fact]
    public async Task TheEmailIsTrimmedBeforeTheLookup()
    {
        FindAgent(Agent());

        await _handler.HandleAsync(new LoginRequest("  " + Email + "  ", Password), CancellationToken.None);

        _agents.Verify(a => a.FindForLoginAsync(Email, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AWrongPassword_IsRejected_AndNoTokenIsIssued()
    {
        FindAgent(Agent());

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _handler.HandleAsync(new LoginRequest(Email, "not-the-password"), CancellationToken.None));

        _tokens.Verify(t => t.Issue(It.IsAny<CurrentUserDto>()), Times.Never);
    }

    [Fact]
    public async Task AnUnknownEmail_IsRejected_AndNoTokenIsIssued()
    {
        _agents.Setup(a => a.FindForLoginAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentLoginRecord?)null);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _handler.HandleAsync(new LoginRequest("nobody@example.com", Password), CancellationToken.None));

        _tokens.Verify(t => t.Issue(It.IsAny<CurrentUserDto>()), Times.Never);
    }

    [Fact]
    public async Task AnInactiveAgent_IsRejected_EvenWithTheRightPassword()
    {
        FindAgent(Agent(isActive: false));

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _handler.HandleAsync(new LoginRequest(Email, Password), CancellationToken.None));

        _tokens.Verify(t => t.Issue(It.IsAny<CurrentUserDto>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task AnAgentWithNoPasswordSet_IsRejected(string? hash)
    {
        FindAgent(AgentWithHash(hash));

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _handler.HandleAsync(new LoginRequest(Email, Password), CancellationToken.None));

        _tokens.Verify(t => t.Issue(It.IsAny<CurrentUserDto>()), Times.Never);
    }

    [Fact]
    public async Task EveryFailure_GivesTheSameMessage_SoNothingIsRevealedAboutWhichEmailsExist()
    {
        // Wrong password for a real agent.
        FindAgent(Agent());
        var wrongPassword = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _handler.HandleAsync(new LoginRequest(Email, "not-the-password"), CancellationToken.None));

        // An email nobody has.
        _agents.Setup(a => a.FindForLoginAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentLoginRecord?)null);
        var unknownEmail = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _handler.HandleAsync(new LoginRequest("nobody@example.com", Password), CancellationToken.None));

        // A real agent who may not sign in.
        FindAgent(Agent(isActive: false));
        var inactive = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _handler.HandleAsync(new LoginRequest(Email, Password), CancellationToken.None));

        Assert.Equal("Invalid email or password.", wrongPassword.Message);
        Assert.Equal(wrongPassword.Message, unknownEmail.Message);
        Assert.Equal(wrongPassword.Message, inactive.Message);
    }

    private static AgentLoginRecord Agent(bool isActive = true) => AgentWithHash(PasswordHash, isActive);

    private static AgentLoginRecord AgentWithHash(string? passwordHash, bool isActive = true) =>
        new(1, "Alex Turner", Email, isActive, passwordHash);

    private void FindAgent(AgentLoginRecord agent) =>
        _agents.Setup(a => a.FindForLoginAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
}
