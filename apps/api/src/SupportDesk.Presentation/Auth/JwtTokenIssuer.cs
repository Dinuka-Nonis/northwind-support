using System.Globalization;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;

namespace SupportDesk.Presentation.Auth;

/// <summary>
/// Issues HMAC-SHA256 signed JSON Web Tokens that carry the agent's id, name and email.
/// </summary>
/// <remarks>
/// The expiry comes from <see cref="IClock"/>, so a test can fix "now" and assert the exact
/// lifetime. The token holds no password and nothing secret: anyone can read its claims, but
/// only the holder of the key can make one the API will accept.
/// </remarks>
public sealed class JwtTokenIssuer : ITokenIssuer
{
    private static readonly JsonWebTokenHandler Handler = new();

    private readonly JwtOptions _options;
    private readonly IClock _clock;
    private readonly SigningCredentials _credentials;

    public JwtTokenIssuer(JwtOptions options, IClock clock)
    {
        _options = options;
        _clock = clock;
        _credentials = new SigningCredentials(options.CreateSigningKey(), SecurityAlgorithms.HmacSha256);
    }

    public IssuedToken Issue(CurrentUserDto user)
    {
        var now = _clock.UtcNow;
        var expiresAtUtc = now.Add(_options.Lifetime);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAtUtc,
            SigningCredentials = _credentials,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(CultureInfo.InvariantCulture),
                [JwtRegisteredClaimNames.Name] = user.FullName,
                [JwtRegisteredClaimNames.Email] = user.Email,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString()
            }
        };

        return new IssuedToken(Handler.CreateToken(descriptor), expiresAtUtc);
    }
}
