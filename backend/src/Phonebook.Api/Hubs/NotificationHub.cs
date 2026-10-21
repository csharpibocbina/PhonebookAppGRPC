using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Phonebook.Api.Auth;
using Phonebook.Application.Notifications;

namespace Phonebook.Api.Hubs;

/// <summary>Methods the server can call on the browser. The client listens with connection.on("Notify", ...).</summary>
public interface INotificationClient
{
    Task Notify(Notification notification);
}

/// <summary>
/// SignalR hub: a long-lived connection (usually a WebSocket) that lets the
/// server push events to the browser. gRPC handles request/response; this
/// handles server-to-browser notifications.
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub<INotificationClient>
{
    public const string Path = "/hubs/notifications";
}

/// <summary>Tells SignalR to use the JWT "sub" claim as the user id, so Clients.User(id) works.</summary>
public sealed class SubClaimUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User.GetUserId()?.ToString();
}

/// <summary>Implements the application's INotificationPublisher with SignalR.</summary>
public sealed class SignalRNotificationPublisher(
    IHubContext<NotificationHub, INotificationClient> hub,
    ILogger<SignalRNotificationPublisher> logger) : INotificationPublisher
{
    public async Task PublishAsync(Guid userId, Notification notification, CancellationToken cancellationToken = default)
    {
        try
        {
            // Every open tab of this user gets the event.
            await hub.Clients.User(userId.ToString()).Notify(notification);
        }
        catch (Exception ex)
        {
            // A failed toast must never fail the gRPC call that caused it.
            logger.LogWarning(ex, "Could not push {Type} notification to user {UserId}", notification.Type, userId);
        }
    }
}
