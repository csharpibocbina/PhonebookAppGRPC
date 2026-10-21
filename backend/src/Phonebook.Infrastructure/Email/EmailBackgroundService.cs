using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Phonebook.Application.Email;
using Phonebook.Application.Notifications;

namespace Phonebook.Infrastructure.Email;

/// <summary>
/// Sends queued emails one by one, retrying up to 3 times, then tells the
/// user's browser over SignalR that the email went out.
/// </summary>
public sealed class EmailBackgroundService(
    ChannelEmailQueue queue,
    IEmailSender sender,
    INotificationPublisher notifications,
    IOptions<SmtpOptions> options,
    TimeProvider time,
    ILogger<EmailBackgroundService> logger) : BackgroundService
{
    public const int MaxAttempts = 3;
    internal TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.ReadAllAsync(stoppingToken))
        {
            await ProcessAsync(message, stoppingToken);
        }
    }

    internal async Task<bool> ProcessAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
            return false;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await sender.SendAsync(message, cancellationToken);
                logger.LogInformation("Sent email '{Subject}' to {To}", message.Subject, message.To);

                if (message.NotifyUserId is { } userId)
                {
                    await notifications.PublishAsync(userId,
                        Notification.Success(NotificationTypes.EmailSent, "Email sent", $"\"{message.Subject}\" was sent to {message.To}.", time),
                        cancellationToken);
                }
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Sending email to {To} failed (attempt {Attempt}/{Max})", message.To, attempt, MaxAttempts);
                if (attempt < MaxAttempts)
                    await Task.Delay(RetryDelay, time, cancellationToken);
            }
        }

        logger.LogError("Giving up on email '{Subject}' to {To}", message.Subject, message.To);
        return false;
    }
}
