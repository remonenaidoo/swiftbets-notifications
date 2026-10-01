using SwiftBets.Notifications.Application.Ports;
using SwiftBets.Notifications.Domain;

namespace SwiftBets.Notifications.Application.Tests;

internal sealed class InMemoryDeliveries : IDeliveryStore
{
    public Dictionary<Guid, Delivery> Rows { get; } = [];

    public Task<bool> ExistsAsync(Guid notificationId, CancellationToken cancellationToken) => Task.FromResult(Rows.ContainsKey(notificationId));

    public Task<bool> RecordAsync(Delivery delivery, CancellationToken cancellationToken) => Task.FromResult(Rows.TryAdd(delivery.NotificationId, delivery));

    public Task<IReadOnlyList<Delivery>> ForUserAsync(Guid userId, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Delivery>>([.. Rows.Values.Where(d => d.UserId == userId).Take(limit)]);
}

internal sealed class InMemoryStatuses : IAccountStatusStore
{
    public Dictionary<Guid, string> Rows { get; } = [];

    public Task<string?> GetAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(Rows.GetValueOrDefault(userId));

    public Task SetAsync(Guid userId, string status, DateTimeOffset changedAt, CancellationToken cancellationToken)
    {
        Rows[userId] = status;
        return Task.CompletedTask;
    }
}

internal sealed class RecordingEmail(bool configured = true, int failures = 0) : IEmailSender
{
    private int _failures = failures;

    public List<(string To, RenderedEmail Email)> Sent { get; } = [];

    public Task<bool> SendAsync(string to, RenderedEmail email, CancellationToken cancellationToken)
    {
        if (_failures-- > 0)
        {
            throw new IOException("smtp unavailable");
        }

        if (configured)
        {
            Sent.Add((to, email));
        }

        return Task.FromResult(configured);
    }
}
