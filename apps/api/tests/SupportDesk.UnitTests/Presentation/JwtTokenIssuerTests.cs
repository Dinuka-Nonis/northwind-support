using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Presentation.Auth;
using SupportDesk.Presentation.Extensions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Presentation;

public class JwtTokenIssuerTests
{
    private const string Key = "unit-test-only-signing-key-0123456789-abcdef";

    private static readonly CurrentUserDto Agent = new(7, "Priya Nair", "priya.nair@northwind-support.example");

    private static JwtOptions Options(string key = Key, int lifetimeMinutes = 60) => new()
    {
        Issuer = "SupportDesk.Api",
        Audience = "SupportDesk.Web",
        Key = key,
        LifetimeMinutes = lifetimeMinutes
    };

    private static JsonWebToken Read(IssuedToken issued) => new JsonWebTokenHandler().ReadJsonWebToken(issued.Token);

    /// <summary>
    /// Checks the signature and nothing else. The fixed clock is in the past, so the lifetime
    /// check would fail for a reason this test is not about.
    /// </summary>
    private static TokenValidationParameters SignatureOnly(string key) => new()
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = false,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = Options(key).CreateSigningKey()
    };

    [Fact]
    public void TheToken_CarriesTheAgentsIdNameAndEmail()
    {
        var token = Read(new JwtTokenIssuer(Options(), new FixedClock()).Issue(Agent));

        Assert.Equal("7", token.GetClaim(JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("Priya Nair", token.GetClaim(JwtRegisteredClaimNames.Name).Value);
        Assert.Equal("priya.nair@northwind-support.example", token.GetClaim(JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("SupportDesk.Api", token.Issuer);
        Assert.Contains("SupportDesk.Web", token.Audiences);
    }

    [Fact]
    public void TheToken_ExpiresSixtyMinutesAfterNow_AndTheResponseSaysSo()
    {
        var clock = new FixedClock();

        var issued = new JwtTokenIssuer(Options(), clock).Issue(Agent);

        var expected = clock.UtcNow.AddMinutes(60);
        Assert.Equal(expected, issued.ExpiresAtUtc);
        Assert.Equal(expected, Read(issued).ValidTo);
    }

    [Fact]
    public void TheLifetime_FollowsTheConfiguration()
    {
        var clock = new FixedClock();

        var issued = new JwtTokenIssuer(Options(lifetimeMinutes: 15), clock).Issue(Agent);

        Assert.Equal(clock.UtcNow.AddMinutes(15), issued.ExpiresAtUtc);
    }

    [Fact]
    public async Task TheToken_IsAcceptedWithTheSigningKey_AndRefusedWithAnother()
    {
        var issued = new JwtTokenIssuer(Options(), new FixedClock()).Issue(Agent);

        var handler = new JsonWebTokenHandler();

        Assert.True((await handler.ValidateTokenAsync(issued.Token, SignatureOnly(Key))).IsValid);
        Assert.False((await handler.ValidateTokenAsync(issued.Token, SignatureOnly("a-different-key-that-is-also-long-enough-0123"))).IsValid);
    }

    [Fact]
    public void TwoTokensForTheSameAgent_AreNotIdentical()
    {
        var issuer = new JwtTokenIssuer(Options(), new FixedClock());

        Assert.NotEqual(issuer.Issue(Agent).Token, issuer.Issue(Agent).Token);
    }

    [Fact]
    public void AValidConfiguration_Passes() => Options().EnsureValid();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("too-short")]
    [InlineData("0123456789012345678901234567890")] // 31 bytes, one under the minimum
    public void AMissingOrShortKey_IsRejected(string key)
    {
        var error = Assert.Throws<InvalidOperationException>(() => Options(key).EnsureValid());

        Assert.Contains("Key", error.Message);

        // The message names the problem, never the value.
        if (!string.IsNullOrWhiteSpace(key))
        {
            Assert.DoesNotContain(key, error.Message);
        }
    }

    [Fact]
    public void AKeyOfExactlyThirtyTwoBytes_IsAccepted() => Options(new string('k', 32)).EnsureValid();

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ALifetimeThatIsNotPositive_IsRejected(int minutes) =>
        Assert.Throws<InvalidOperationException>(() => Options(lifetimeMinutes: minutes).EnsureValid());

    [Fact]
    public void StartupStops_WhenTheKeyIsMissingFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "SupportDesk.Api",
                ["Jwt:Audience"] = "SupportDesk.Web",
                ["Jwt:LifetimeMinutes"] = "60"
            })
            .Build();

        var error = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddJwtAuthentication(configuration));

        Assert.Contains("Key", error.Message);
    }

    [Fact]
    public void StartupStops_WhenTheKeyIsTooShort()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "SupportDesk.Api",
                ["Jwt:Audience"] = "SupportDesk.Web",
                ["Jwt:LifetimeMinutes"] = "60",
                ["Jwt:Key"] = "too-short"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddJwtAuthentication(configuration));
    }
}
