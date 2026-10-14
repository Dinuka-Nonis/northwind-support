namespace SupportDesk.Application.Exceptions;

/// <summary>
/// Thrown when a sign-in fails, whatever the reason. Surfaces as HTTP 401.
/// </summary>
/// <remarks>
/// The message is the same for every cause (unknown email, wrong password, inactive agent, no
/// password set) so the response never reveals which email addresses exist. The real reason is
/// written to the log by the login handler instead.
/// </remarks>
public sealed class InvalidCredentialsException() : Exception(DefaultMessage)
{
    public const string DefaultMessage = "Invalid email or password.";
}
