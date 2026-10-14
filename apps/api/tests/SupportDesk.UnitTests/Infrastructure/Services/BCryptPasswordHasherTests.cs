using SupportDesk.Infrastructure.Services;

namespace SupportDesk.UnitTests.Infrastructure.Services;

public class BCryptPasswordHasherTests
{
    private const string Password = "Local_Dev_Agent1";

    private readonly BCryptPasswordHasher _hasher = new();

    [Fact]
    public void Verify_AcceptsTheRightPassword()
    {
        var hash = _hasher.Hash(Password);

        Assert.True(_hasher.Verify(hash, Password));
    }

    [Fact]
    public void Verify_RejectsAWrongPassword()
    {
        var hash = _hasher.Hash(Password);

        Assert.False(_hasher.Verify(hash, "not-the-password"));
    }

    [Fact]
    public void Verify_IsCaseSensitive()
    {
        var hash = _hasher.Hash(Password);

        Assert.False(_hasher.Verify(hash, Password.ToUpperInvariant()));
    }

    [Fact]
    public void Hash_IsNotThePlainText()
    {
        var hash = _hasher.Hash(Password);

        Assert.DoesNotContain(Password, hash);
        Assert.NotEqual(Password, hash);
    }

    [Fact]
    public void Hash_DiffersEachTimeBecauseOfTheSalt()
    {
        var first = _hasher.Hash(Password);
        var second = _hasher.Hash(Password);

        Assert.NotEqual(first, second);
        Assert.True(_hasher.Verify(first, Password));
        Assert.True(_hasher.Verify(second, Password));
    }

    [Fact]
    public void Hash_RejectsAnEmptyPassword() =>
        Assert.Throws<ArgumentException>(() => _hasher.Hash(string.Empty));

    [Theory]
    [InlineData("")]
    [InlineData("not-a-bcrypt-hash")]
    public void Verify_NeverMatchesAHashThatIsEmptyOrMalformed(string badHash) =>
        Assert.False(_hasher.Verify(badHash, Password));
}
