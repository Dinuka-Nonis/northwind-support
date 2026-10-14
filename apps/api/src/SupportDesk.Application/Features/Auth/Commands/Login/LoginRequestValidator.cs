using FluentValidation;
using SupportDesk.Application.Contracts.Auth;

namespace SupportDesk.Application.Features.Auth.Commands.Login;

/// <summary>
/// Rejects an empty or oversized sign-in before any lookup or hashing happens. The password
/// limit also stops someone from making the server hash a huge string. The web form checks the
/// same numbers.
/// </summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    /// <summary>Matches the width of the Agents.Email column.</summary>
    public const int EmailMaxLength = 256;

    public const int PasswordMaxLength = 128;

    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(EmailMaxLength).WithMessage($"Email must be at most {EmailMaxLength} characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MaximumLength(PasswordMaxLength).WithMessage($"Password must be at most {PasswordMaxLength} characters.");
    }
}
