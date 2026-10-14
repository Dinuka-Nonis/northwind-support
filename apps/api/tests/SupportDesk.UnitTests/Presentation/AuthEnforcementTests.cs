using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Presentation.Auth;
using SupportDesk.Presentation.Extensions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Presentation;

/// <summary>
/// Proves that the API refuses a call without a valid token, and accepts one with it. The host
/// is a small one built here, but it calls the real <c>AddJwtAuthentication</c>, so what is
/// tested is the production setup: the validation rules and the fallback policy.
/// </summary>
/// <remarks>
/// <c>/protected</c> carries no <c>[Authorize]</c> of its own. If it answers 401, that is the
/// fallback policy at work, which is exactly what protects every real controller.
/// </remarks>
public sealed class AuthEnforcementTests : IAsyncLifetime
{
    private const string Key = "unit-test-only-signing-key-0123456789-abcdef";
    private const string OtherKey = "a-different-key-that-is-also-long-enough-0123";
    private const string Issuer = "SupportDesk.Api";
    private const string Audience = "SupportDesk.Web";

    private static readonly CurrentUserDto Agent = new(7, "Priya Nair", "priya.nair@northwind-support.example");

    private IHost _host = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
                ["Jwt:LifetimeMinutes"] = "60",
                ["Jwt:Key"] = Key
            })
            .Build();

        _host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddJwtAuthentication(configuration);
                })
                .Configure(app =>
                {
                    // The same order as Program.cs: authenticate, then authorize, then the endpoints.
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        // No [Authorize] here on purpose: only the fallback policy can close it.
                        endpoints.MapGet("/protected", (HttpContext context) => context.User.GetAgentName());
                        endpoints.MapGet("/open", () => "open").AllowAnonymous();
                    });
                }))
            .StartAsync();

        _client = _host.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    /// <summary>
    /// A token made the way the API makes them. The issue time is the real clock, because the
    /// bearer handler checks expiry against the real clock too.
    /// </summary>
    private static string TokenIssuedAt(DateTime issuedAtUtc, string key = Key)
    {
        var options = new JwtOptions { Issuer = Issuer, Audience = Audience, Key = key, LifetimeMinutes = 60 };

        return new JwtTokenIssuer(options, new FixedClock(issuedAtUtc)).Issue(Agent).Token;
    }

    private Task<HttpResponseMessage> GetAsync(string path, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return _client.SendAsync(request);
    }

    [Fact]
    public async Task AnEndpointWithoutAToken_Gives401()
    {
        var response = await GetAsync("/protected");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnEndpointWithAValidToken_Gives200_AndKnowsWhoIsCalling()
    {
        var response = await GetAsync("/protected", TokenIssuedAt(DateTime.UtcNow));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Priya Nair", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AnExpiredToken_Gives401()
    {
        // Issued two hours ago with a 60 minute lifetime: it ended an hour ago.
        var expired = TokenIssuedAt(DateTime.UtcNow.AddHours(-2));

        var response = await GetAsync("/protected", expired);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ATamperedToken_Gives401()
    {
        var parts = TokenIssuedAt(DateTime.UtcNow).Split('.');

        // Change one character in the middle of the signature. The payload is untouched, so the
        // token still reads fine, but it no longer matches what the key would have signed.
        var signature = parts[2].ToCharArray();
        signature[10] = signature[10] == 'A' ? 'B' : 'A';
        var tampered = $"{parts[0]}.{parts[1]}.{new string(signature)}";

        var response = await GetAsync("/protected", tampered);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ATokenSignedWithAnotherKey_Gives401()
    {
        var forged = TokenIssuedAt(DateTime.UtcNow, OtherKey);

        var response = await GetAsync("/protected", forged);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ATokenThatIsNotAToken_Gives401()
    {
        var response = await GetAsync("/protected", "not-a-jwt");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnEndpointMarkedAnonymous_Gives200_WithoutAToken()
    {
        var response = await GetAsync("/open");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("open", await response.Content.ReadAsStringAsync());
    }
}
