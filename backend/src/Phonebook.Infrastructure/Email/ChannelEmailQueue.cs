using System.Threading.Channels;
using Phonebook.Application.Email;

namespace Phonebook.Infrastructure.Email;

/// <summary>An in-memory queue. Requests write to it; EmailBackgroundService reads from it.</summary>
public sealed class ChannelEmailQueue : IEmailQueue
{
    private readonly Channel<EmailMessage> _channel =
        Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.DropOldest });

    public ValueTask QueueAsync(EmailMessage message, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(message, cancellationToken);

    public IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
