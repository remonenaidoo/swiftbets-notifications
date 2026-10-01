using Dapper;
using Npgsql;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.Notifications.Application.Ports;
using SwiftBets.Notifications.Domain;

namespace SwiftBets.Notifications.Infrastructure.Persistence;

public sealed class PostgresDeliveryStore(NpgsqlDataSource dataSource) : IDeliveryStore
{
    private static readonly SqlResources Sql = SqlResources.For<PostgresDeliveryStore>();

    public async Task<bool> ExistsAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(Sql.Get("Deliveries.Exists"), new { NotificationId = notificationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<bool> RecordAsync(Delivery delivery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var inserted = await connection.ExecuteAsync(new CommandDefinition(
            Sql.Get("Deliveries.Record"),
            new { delivery.NotificationId, delivery.UserId, delivery.Channel, delivery.Template, Status = (short)delivery.Status, delivery.Reason, delivery.RecordedAt },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return inserted == 1;
    }

    public async Task<IReadOnlyList<Delivery>> ForUserAsync(Guid userId, int limit, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(Sql.Get("Deliveries.ForUser"), new { UserId = userId, Limit = limit }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return [.. rows.Select(r => new Delivery(r.NotificationId, r.UserId, r.Channel, r.Template, (DeliveryStatus)r.Status, r.Reason, new DateTimeOffset(DateTime.SpecifyKind(r.RecordedAt, DateTimeKind.Utc))))];
    }

    // Npgsql reads timestamptz as a UTC DateTime.
    private sealed record Row(Guid NotificationId, Guid UserId, string Channel, string Template, short Status, string? Reason, DateTime RecordedAt);
}
