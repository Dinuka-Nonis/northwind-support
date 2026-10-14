using BCryptNet = BCrypt.Net.BCrypt;
using SupportDesk.Application.Abstractions;

namespace SupportDesk.Infrastructure.Services;

/// <summary>
/// Password hashing with BCrypt, through the maintained BCrypt.Net-Next library.
/// </summary>
/// <remarks>
/// <para>
/// BCrypt is deliberately slow and adds a random salt to every hash, so two agents with the
/// same password get different hashes and a stolen table cannot be attacked with a ready-made
/// lookup. The salt and the work factor are stored inside the hash string, so nothing else
/// has to be kept.
/// </para>
/// <para>
/// Enhanced mode pre-hashes the password with SHA-384 before BCrypt sees it. Plain BCrypt
/// reads only the first 72 bytes of a password; this removes that limit.
/// </para>
/// <para>
/// Work factor 12 means 2^12 rounds, roughly a quarter of a second per hash on a current
/// machine. That is unnoticeable for one login and costly for someone guessing millions of
/// passwords. Raise it as hardware gets faster; old hashes keep working because each carries
/// its own factor.
/// </para>
/// </remarks>
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        return BCryptNet.EnhancedHashPassword(password, WorkFactor);
    }

    public bool Verify(string hash, string password)
    {
        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        try
        {
            return BCryptNet.EnhancedVerify(password, hash);
        }
        catch (Exception ex) when (ex is BCrypt.Net.SaltParseException or ArgumentException or FormatException)
        {
            // Not a BCrypt hash at all (for example, a corrupted column). It cannot match.
            return false;
        }
    }
}
