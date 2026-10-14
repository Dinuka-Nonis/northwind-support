using SupportDesk.Application.Contracts.Auth;

namespace SupportDesk.Application.Abstractions;

/// <summary>
/// Creates the access token handed to an agent who has signed in.
/// </summary>
/// <remarks>
/// The application only knows this seam. The token format, the signing key and the lifetime are
/// decisions of the web layer, so the login use case can be tested without any of them.
/// </remarks>
public interface ITokenIssuer
{
    /// <summary>Issues a signed token that identifies <paramref name="user"/> until it expires.</summary>
    IssuedToken Issue(CurrentUserDto user);
}
