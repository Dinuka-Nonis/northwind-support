using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SupportDesk.Presentation.Auth;

/// <summary>
/// The settings that decide how access tokens are signed, who they are for and how long they
/// last. Bound from the <c>Jwt</c> configuration section.
/// </summary>
/// <remarks>
/// Nothing here has a default, on purpose: the signing key in particular must come from
/// configuration (an environment variable, <c>Jwt__Key</c>, in any real deployment), and a
/// missing or weak value stops the app at startup instead of signing tokens with a hidden one.
/// </remarks>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 needs a key of at least 256 bits.</summary>
    public const int MinimumKeyBytes = 32;

    /// <summary>Who issues the tokens. A token with another issuer is refused.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Who the tokens are for. A token with another audience is refused.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>The secret the tokens are signed with. At least 32 bytes. Never commit a real one.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>How long a token is accepted after it is issued.</summary>
    public int LifetimeMinutes { get; set; }

    public TimeSpan Lifetime => TimeSpan.FromMinutes(LifetimeMinutes);

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(Key));

    /// <summary>Throws if the settings cannot produce safe tokens. Run at startup.</summary>
    public void EnsureValid()
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(Issuer))
        {
            problems.Add("Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(Audience))
        {
            problems.Add("Audience is required.");
        }

        if (string.IsNullOrWhiteSpace(Key))
        {
            problems.Add("Key is required. Set it in configuration or with the Jwt__Key environment variable.");
        }
        else if (Encoding.UTF8.GetByteCount(Key) < MinimumKeyBytes)
        {
            // The key itself is never put in the message.
            problems.Add($"Key must be at least {MinimumKeyBytes} bytes long.");
        }

        if (LifetimeMinutes <= 0)
        {
            problems.Add("LifetimeMinutes must be greater than 0.");
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException("Invalid 'Jwt' configuration: " + string.Join(" ", problems));
        }
    }
}
