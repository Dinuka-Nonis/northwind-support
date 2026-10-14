using System.Globalization;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace SupportDesk.Presentation.Extensions;

/// <summary>
/// Reads the signed-in agent out of the validated token. The claim names are the short JWT
/// ones, because the bearer handler is set not to rename them (see
/// <see cref="AuthenticationExtensions"/>).
/// </summary>
/// <remarks>
/// These throw when a claim is missing. That cannot happen for a token this API issued and
/// validated, so it would mean a bug, and a 500 is the honest answer to a bug.
/// </remarks>
public static class ClaimsPrincipalExtensions
{
    public static int GetAgentId(this ClaimsPrincipal user)
    {
        var value = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? id
            : throw new InvalidOperationException("The token does not carry a valid agent id.");
    }

    public static string GetAgentName(this ClaimsPrincipal user) =>
        user.FindFirst(JwtRegisteredClaimNames.Name)?.Value
        ?? throw new InvalidOperationException("The token does not carry an agent name.");

    public static string GetAgentEmail(this ClaimsPrincipal user) =>
        user.FindFirst(JwtRegisteredClaimNames.Email)?.Value
        ?? throw new InvalidOperationException("The token does not carry an agent email.");
}
