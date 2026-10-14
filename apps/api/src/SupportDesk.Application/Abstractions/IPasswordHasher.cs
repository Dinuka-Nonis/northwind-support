namespace SupportDesk.Application.Abstractions;

/// <summary>
/// Turns a password into a salted hash and checks a password against one.
/// </summary>
/// <remarks>
/// The application only knows this seam. Which algorithm sits behind it is an infrastructure
/// decision, so it can change without touching the login code.
/// </remarks>
public interface IPasswordHasher
{
    /// <summary>Hashes the password with a fresh random salt. The same password gives a different hash each time.</summary>
    string Hash(string password);

    /// <summary>
    /// True when the password matches the hash. A hash that is empty or not in the expected
    /// format never matches; it does not throw.
    /// </summary>
    bool Verify(string hash, string password);
}
