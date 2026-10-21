using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using Phonebook.Application.Contacts;
using Phonebook.Domain.Entities;

namespace Phonebook.UnitTests.TestHelpers;

public static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);

    /// <summary>A FakeItEasy fake of TimeProvider that always returns <see cref="Now"/>.</summary>
    public static TimeProvider FixedTime(DateTimeOffset? now = null)
    {
        var time = A.Fake<TimeProvider>();
        A.CallTo(() => time.GetUtcNow()).Returns(now ?? Now);
        return time;
    }

    public static ContactInput ValidInput(string firstName = "Maria") =>
        new(firstName, "Santos", "+63 917 555 0101", "maria@example.com", "Manila", "Met at work", true);

    public static Contact Contact(Guid ownerId, string firstName = "Maria", string lastName = "Santos") => new()
    {
        OwnerId = ownerId,
        FirstName = firstName,
        LastName = lastName,
        PhoneNumber = "+63 917 555 0101",
        CreatedAt = Now,
        UpdatedAt = Now,
    };

    public static User User(string email = "jay@example.com") => new()
    {
        Email = email,
        DisplayName = "Jay",
        PasswordHash = "hashed",
        CreatedAt = Now,
    };

    public static HttpContext HttpContextFor(Guid userId) => new DefaultHttpContext
    {
        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())], "Test")),
    };
}
