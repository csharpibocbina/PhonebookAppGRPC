namespace Phonebook.Infrastructure.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    /// <summary>"papercut" inside docker-compose, "localhost" when running the API on your machine.</summary>
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 2525;
    public string From { get; set; } = "no-reply@phonebook.local";
    public string FromName { get; set; } = "Phonebook";
    public bool Enabled { get; set; } = true;
}
