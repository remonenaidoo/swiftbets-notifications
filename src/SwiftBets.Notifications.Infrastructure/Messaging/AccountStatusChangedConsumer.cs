using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Contracts.Identity;
using SwiftBets.Notifications.Application.Ports;

namespace SwiftBets.Notifications.Infrastructure.Messaging;

/// <summary>Keeps the account status the marketing check reads; out-of-order events never overwrite a newer status.</summary>
public sealed class AccountStatusChangedConsumer(IAccountStatusStore statuses) : IEventHandler<AccountStatusChangedV1>
{
    public Task HandleAsync(ConsumedEvent<AccountStatusChangedV1> message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var payload = message.Envelope.Payload;
        return statuses.SetAsync(payload.UserId, payload.Status.ToString(), payload.ChangedAt, cancellationToken);
    }
}
