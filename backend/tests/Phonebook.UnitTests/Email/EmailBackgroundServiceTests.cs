using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Phonebook.Application.Email;
using Phonebook.Application.Notifications;
using Phonebook.Infrastructure.Email;
using Phonebook.UnitTests.TestHelpers;

namespace Phonebook.UnitTests.Email;

public class EmailBackgroundServiceTests
{
    private readonly IEmailSender _sender = A.Fake<IEmailSender>();
    private readonly INotificationPublisher _notifications = A.Fake<INotificationPublisher>();
    private readonly EmailMessage _message = new("jay@example.com", "Welcome to Phonebook", "<p>Hi</p>", "Hi", Guid.NewGuid());

    private EmailBackgroundService CreateSut(bool enabled = true) => new(
        new ChannelEmailQueue(), _sender, _notifications,
        Options.Create(new SmtpOptions { Enabled = enabled }),
        TimeProvider.System, NullLogger<EmailBackgroundService>.Instance)
    {
        RetryDelay = TimeSpan.Zero,
    };

    [Fact]
    public async Task Sends_email_and_notifies_the_user()
    {
        var sent = await CreateSut().ProcessAsync(_message, CancellationToken.None);

        sent.ShouldBeTrue();
        A.CallTo(() => _sender.SendAsync(_message, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _notifications.PublishAsync(_message.NotifyUserId!.Value,
            A<Notification>.That.Matches(n => n.Type == NotificationTypes.EmailSent), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Retries_when_smtp_fails_then_succeeds()
    {
        A.CallTo(() => _sender.SendAsync(A<EmailMessage>._, A<CancellationToken>._))
            .Throws(new IOException("SMTP down")).Once();

        var sent = await CreateSut().ProcessAsync(_message, CancellationToken.None);

        sent.ShouldBeTrue();
        A.CallTo(() => _sender.SendAsync(_message, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task Gives_up_after_three_attempts_without_notifying()
    {
        A.CallTo(() => _sender.SendAsync(A<EmailMessage>._, A<CancellationToken>._)).Throws(new IOException("SMTP down"));

        var sent = await CreateSut().ProcessAsync(_message, CancellationToken.None);

        sent.ShouldBeFalse();
        A.CallTo(() => _sender.SendAsync(_message, A<CancellationToken>._)).MustHaveHappened(EmailBackgroundService.MaxAttempts, Times.Exactly);
        A.CallTo(() => _notifications.PublishAsync(A<Guid>._, A<Notification>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Does_nothing_when_email_is_disabled()
    {
        await CreateSut(enabled: false).ProcessAsync(_message, CancellationToken.None);

        A.CallTo(() => _sender.SendAsync(A<EmailMessage>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public void Welcome_template_html_encodes_the_display_name()
    {
        var user = TestData.User();
        user.DisplayName = "<script>";

        var message = EmailTemplates.Welcome(user);

        message.HtmlBody.ShouldContain("&lt;script&gt;");
        message.HtmlBody.ShouldNotContain("<script>");
    }
}
