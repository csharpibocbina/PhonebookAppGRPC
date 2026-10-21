using Phonebook.Domain.Entities;

namespace Phonebook.Application.Email;

/// <param name="NotifyUserId">If set, this user gets an "email sent" toast once the email is delivered.</param>
public sealed record EmailMessage(string To, string Subject, string HtmlBody, string TextBody, Guid? NotifyUserId = null);

/// <summary>
/// Emails are queued, not sent inline, so a slow or offline SMTP server never
/// slows down or breaks a login. A background service drains the queue.
/// </summary>
public interface IEmailQueue
{
    ValueTask QueueAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public static class EmailTemplates
{
    public static EmailMessage Welcome(User user) => new(
        user.Email,
        "Welcome to Phonebook",
        Layout($"""
            <h1 style="margin:0 0 12px;font-size:22px">Welcome, {Encode(user.DisplayName)}!</h1>
            <p>Your Phonebook account is ready. Sign in to start adding contacts.</p>
            <p style="color:#64748b;font-size:13px">Every call the app makes to the server is a gRPC call. Have fun exploring!</p>
            """),
        $"Welcome, {user.DisplayName}!\n\nYour Phonebook account is ready. Sign in to start adding contacts.",
        user.Id);

    public static EmailMessage SignInAlert(User user, DateTimeOffset when) => new(
        user.Email,
        "New sign-in to your Phonebook account",
        Layout($"""
            <h1 style="margin:0 0 12px;font-size:22px">New sign-in</h1>
            <p>Hi {Encode(user.DisplayName)}, your account was just signed in to at <strong>{when:yyyy-MM-dd HH:mm} UTC</strong>.</p>
            <p style="color:#64748b;font-size:13px">If this wasn't you, change your password.</p>
            """),
        $"Hi {user.DisplayName}, your account was just signed in to at {when:yyyy-MM-dd HH:mm} UTC. If this wasn't you, change your password.",
        user.Id);

    private static string Layout(string content) => $"""
        <div style="font-family:Segoe UI,Arial,sans-serif;background:#f1f5f9;padding:32px">
          <div style="max-width:520px;margin:auto;background:#fff;border-radius:16px;padding:32px;color:#0f172a">
            <div style="font-weight:700;color:#4f46e5;margin-bottom:16px">📇 Phonebook</div>
            {content}
          </div>
        </div>
        """;

    private static string Encode(string value) => System.Net.WebUtility.HtmlEncode(value);
}
