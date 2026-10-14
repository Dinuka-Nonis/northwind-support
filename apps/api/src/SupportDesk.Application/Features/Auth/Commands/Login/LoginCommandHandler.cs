using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;

namespace SupportDesk.Application.Features.Auth.Commands.Login;

/// <summary>
/// Signs an agent in: finds them by email, checks they may sign in and that the password
/// matches, then asks for a token.
/// </summary>
/// <remarks>
/// Every way of failing throws the same <see cref="InvalidCredentialsException"/>, so a caller
/// cannot tell an unknown email from a wrong password. The real reason goes to the log, with the
/// agent id when there is one. The email that was typed is not logged for an unknown address,
/// because people sometimes paste a password into that box.
/// </remarks>
public sealed class LoginCommandHandler(
    IAgentQueries agents,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer,
    ILogger<LoginCommandHandler> logger)
{
    /// <exception cref="InvalidCredentialsException">
    /// The email is unknown, the agent is inactive or has no password, or the password is wrong.
    /// </exception>
    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var agent = await agents.FindForLoginAsync(request.Email.Trim(), ct);

        if (agent is null)
        {
            logger.LogWarning("Sign-in refused: no agent has that email.");
            throw new InvalidCredentialsException();
        }

        if (!agent.IsActive)
        {
            logger.LogWarning("Sign-in refused for agent {AgentId}: the agent is inactive.", agent.Id);
            throw new InvalidCredentialsException();
        }

        if (string.IsNullOrEmpty(agent.PasswordHash))
        {
            logger.LogWarning("Sign-in refused for agent {AgentId}: no password has been set.", agent.Id);
            throw new InvalidCredentialsException();
        }

        if (!passwordHasher.Verify(agent.PasswordHash, request.Password))
        {
            logger.LogWarning("Sign-in refused for agent {AgentId}: wrong password.", agent.Id);
            throw new InvalidCredentialsException();
        }

        var user = new CurrentUserDto(agent.Id, agent.FullName, agent.Email);
        var token = tokenIssuer.Issue(user);

        logger.LogInformation("Agent {AgentId} signed in.", agent.Id);

        return new LoginResponse(token.Token, token.ExpiresAtUtc, user);
    }
}
