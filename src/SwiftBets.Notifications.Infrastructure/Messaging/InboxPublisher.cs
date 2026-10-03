using SwiftBets.BuildingBlocks.Core;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Contracts.Messaging;
using SwiftBets.Contracts.Notifications;
using SwiftBets.Notifications.Application.Ports;

namespace SwiftBets.Notifications.Infrastructure.Messaging;

/// <summary>Published after the inbox row is stored; a lost push only means the customer sees it on their next refresh.</summary>
public sealed class InboxPublisher(IEventPublisher publisher) : IInboxPublisher
{
    public Task PublishAsync(InboxItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        var payload = new InboxMessageV1(item.MessageId, item.UserId, item.Category, item.Title, item.Body, item.CreatedAt);
        return publisher.PublishAsync(Topics.InboxMessage, item.UserId.ToString(),
            EventEnvelope<InboxMessageV1>.Create(payload, item.CreatedAt, CorrelationContext.CorrelationId ?? CorrelationContext.NewId()), cancellationToken);
    }
}
