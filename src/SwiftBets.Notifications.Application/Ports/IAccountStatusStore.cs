namespace SwiftBets.Notifications.Application.Ports;

/// <summary>The latest known account status per user, projected from identity's status events.</summary>
public interface IAccountStatusStore
{
    Task<string?> GetAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Applies the change only if it is newer than what is stored.</summary>
    Task SetAsync(Guid userId, string status, DateTimeOffset changedAt, CancellationToken cancellationToken);
}
