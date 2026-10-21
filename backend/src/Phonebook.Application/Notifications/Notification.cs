namespace Phonebook.Application.Notifications;

public static class NotificationTypes
{
    public const string ContactCreated = "ContactCreated";
    public const string ContactUpdated = "ContactUpdated";
    public const string ContactDeleted = "ContactDeleted";
    public const string SignedIn = "SignedIn";
    public const string EmailSent = "EmailSent";
}

public static class NotificationLevels
{
    public const string Success = "success";
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Error = "error";
}

/// <summary>An event pushed to the browser over SignalR and shown as a toast.</summary>
public sealed record Notification(string Id, string Type, string Title, string Message, string Level, DateTimeOffset CreatedAt)
{
    public static Notification Create(string type, string title, string message, string level, TimeProvider time) =>
        new(Guid.NewGuid().ToString("N"), type, title, message, level, time.GetUtcNow());

    public static Notification Success(string type, string title, string message, TimeProvider time) =>
        Create(type, title, message, NotificationLevels.Success, time);

    public static Notification Info(string type, string title, string message, TimeProvider time) =>
        Create(type, title, message, NotificationLevels.Info, time);

    public static Notification Warning(string type, string title, string message, TimeProvider time) =>
        Create(type, title, message, NotificationLevels.Warning, time);
}

/// <summary>
/// Sends a notification to every open tab of one user. The API implements this
/// with SignalR; the application layer only sees this interface.
/// </summary>
public interface INotificationPublisher
{
    Task PublishAsync(Guid userId, Notification notification, CancellationToken cancellationToken = default);
}
