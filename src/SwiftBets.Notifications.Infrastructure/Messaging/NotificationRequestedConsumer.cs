using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Contracts.Notifications;
using SwiftBets.Notifications.Application;

namespace SwiftBets.Notifications.Infrastructure.Messaging;

public sealed class NotificationRequestedConsumer(SendNotificationHandler handler) : IEventHandler<NotificationRequestedV1>
{
    public async Task HandleAsync(ConsumedEvent<NotificationRequestedV1> message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var payload = message.Envelope.Payload;
        var request = new NotificationRequest(
            payload.NotificationId, payload.UserId, payload.Channel.ToString(), payload.Template, payload.Recipient, payload.Data ?? new Dictionary<string, string>(), payload.IsMarketing);
        if (await handler.HandleAsync(request, cancellationToken).ConfigureAwait(false) is { } status)
        {
            NotificationMetrics.Deliveries.WithLabels(request.Channel, request.Template, status.ToString()).Inc();
        }
    }
}
