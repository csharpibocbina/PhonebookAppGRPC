using Phonebook.Infrastructure.Auth;

namespace Phonebook.UnitTests.Auth;

public class PasswordHasherTests
{
    private readonly PasswordHasher _sut = new();

    [Fact]
    public void Hash_verifies_only_the_original_password()
    {
        var hash = _sut.Hash("Secret123");

        hash.ShouldNotContain("Secret123");
        _sut.Verify(hash, "Secret123").ShouldBeTrue();
        _sut.Verify(hash, "secret123").ShouldBeFalse();
    }
}
