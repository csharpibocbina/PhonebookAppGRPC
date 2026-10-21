namespace Phonebook.Application.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "phonebook-api";
    public string Audience { get; set; } = "phonebook-web";

    /// <summary>HMAC-SHA256 key. At least 32 bytes. Comes from configuration, never source control.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;

    /// <summary>Set to false only for plain-http local setups that aren't on localhost.</summary>
    public bool RefreshCookieSecure { get; set; } = true;
}
