using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Features.Auth.Commands.Login;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    private static LoginRequest Request(string? email = "alex.turner@northwind-support.example", string? password = "Local_Dev_Agent1") =>
        new(email!, password!);

    private bool IsValid(LoginRequest request) => _validator.Validate(request).IsValid;

    private IEnumerable<string> FailedFields(LoginRequest request) =>
        _validator.Validate(request).Errors.Select(failure => failure.PropertyName).Distinct();

    [Fact]
    public void AValidRequest_Passes() => Assert.True(IsValid(Request()));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankEmail_IsRejected(string? email)
    {
        var request = Request(email: email);

        Assert.False(IsValid(request));
        Assert.Contains("Email", FailedFields(request));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AMissingPassword_IsRejected(string? password)
    {
        var request = Request(password: password);

        Assert.False(IsValid(request));
        Assert.Contains("Password", FailedFields(request));
    }

    [Theory]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void ThePassword_MayBeAtMost128Characters(int length, bool expected) =>
        Assert.Equal(expected, IsValid(Request(password: new string('p', length))));

    [Theory]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public void TheEmail_MayBeAtMost256Characters(int length, bool expected) =>
        Assert.Equal(expected, IsValid(Request(email: new string('e', length))));
}
