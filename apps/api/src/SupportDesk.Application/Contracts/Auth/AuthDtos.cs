namespace SupportDesk.Application.Contracts.Auth;

/// <summary>Body of POST /api/auth/login.</summary>
/// <param name="Email">The agent's email address, up to 256 characters.</param>
/// <param name="Password">The password as typed, up to 128 characters. Never stored or logged.</param>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Who is signed in: just enough to greet them and to identify them on later calls.</summary>
public sealed record CurrentUserDto(int Id, string FullName, string Email);

/// <summary>A freshly created token and the moment it stops being accepted.</summary>
public sealed record IssuedToken(string Token, DateTime ExpiresAtUtc);

/// <summary>What a successful login returns: the token, its expiry and the signed-in agent.</summary>
public sealed record LoginResponse(string Token, DateTime ExpiresAtUtc, CurrentUserDto User);

/// <summary>
/// What the login check needs to know about an agent, including the password hash. Internal to
/// the sign-in flow: it is never returned from an endpoint.
/// </summary>
public sealed record AgentLoginRecord(int Id, string FullName, string Email, bool IsActive, string? PasswordHash);
