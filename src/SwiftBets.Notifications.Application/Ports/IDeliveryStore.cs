using SwiftBets.Notifications.Domain;

namespace SwiftBets.Notifications.Application.Ports;

public interface IDeliveryStore
{
    Task<bool> ExistsAsync(Guid notificationId, CancellationToken cancellationToken);

    /// <summary>False when a delivery for the same notification was already recorded.</summary>
    Task<bool> RecordAsync(Delivery delivery, CancellationToken cancellationToken);

    Task<IReadOnlyList<Delivery>> ForUserAsync(Guid userId, int limit, CancellationToken cancellationToken);
}
