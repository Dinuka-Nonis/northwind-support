using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Presentation.Auth;

namespace SupportDesk.Presentation.Extensions;

public static class AuthenticationExtensions
{
    /// <summary>
    /// Registers token issuing, bearer-token validation and the rule that every endpoint needs a
    /// signed-in user. The <c>Jwt</c> settings are read and checked here, so a missing or weak key
    /// stops the app at startup.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("The 'Jwt' configuration section is missing.");
        options.EnsureValid();

        services.AddSingleton(options);
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(bearer =>
            {
                // Keep the claim names as issued ("sub", "name", "email") instead of renaming
                // them to long .NET URIs, so ClaimsPrincipalExtensions reads what the issuer wrote.
                bearer.MapInboundClaims = false;

                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = options.CreateSigningKey(),

                    // Only the algorithm the issuer uses, so a token cannot pick a weaker one.
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],

                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,

                    ValidateAudience = true,
                    ValidAudience = options.Audience,

                    ValidateLifetime = true,
                    RequireExpirationTime = true,

                    // No grace period: a token stops working at the expiry the client was told.
                    ClockSkew = TimeSpan.Zero,

                    NameClaimType = JwtRegisteredClaimNames.Name
                };
            });

        services.AddAuthorization(authorization =>
        {
            // The fallback policy applies to every endpoint that has no authorization of its own,
            // so a controller added tomorrow is closed without anyone remembering [Authorize].
            // Opening an endpoint is the deliberate act: [AllowAnonymous], as on POST /api/auth/login.
            // Swagger stays open because it is middleware, not an endpoint, so this policy never sees it.
            authorization.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }
}
