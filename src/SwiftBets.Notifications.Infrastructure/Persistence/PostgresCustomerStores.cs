using Dapper;
using Npgsql;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.Notifications.Application.Ports;
using SwiftBets.Notifications.Domain;

namespace SwiftBets.Notifications.Infrastructure.Persistence;

public sealed class PostgresCustomerInbox(NpgsqlDataSource dataSource) : ICustomerInbox
{
    private static readonly SqlResources Sql = SqlResources.For<PostgresCustomerInbox>();

    public async Task<bool> AddAsync(InboxItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(new CommandDefinition(Sql.Get("Inbox.Add"), new { item.MessageId, item.UserId, item.Category, item.Title, item.Body, item.CreatedAt }, cancellationToken: cancellationToken)).ConfigureAwait(false) == 1;
    }

    public async Task<IReadOnlyList<InboxItem>> ForUserAsync(Guid userId, int limit, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(Sql.Get("Inbox.ForUser"), new { UserId = userId, Limit = limit }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return [.. rows.Select(r => new InboxItem(r.MessageId, r.UserId, r.Category, r.Title, r.Body, Utc(r.CreatedAt), r.ReadAt is { } read ? Utc(read) : null))];
    }

    public async Task<bool> MarkReadAsync(Guid userId, Guid messageId, DateTimeOffset readAt, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(new CommandDefinition(Sql.Get("Inbox.MarkRead"), new { UserId = userId, MessageId = messageId, ReadAt = readAt }, cancellationToken: cancellationToken)).ConfigureAwait(false) == 1;
    }

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    // Npgsql reads timestamptz as a UTC DateTime.
    private sealed class Row
    {
        public Guid MessageId { get; init; }

        public Guid UserId { get; init; }

        public string Category { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string Body { get; init; } = string.Empty;

        public DateTime CreatedAt { get; init; }

        public DateTime? ReadAt { get; init; }
    }
}

public sealed class PostgresPreferenceStore(NpgsqlDataSource dataSource) : IPreferenceStore
{
    private static readonly SqlResources Sql = SqlResources.For<PostgresPreferenceStore>();

    public async Task<IReadOnlyDictionary<CustomerEvent, ChannelPreference>> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(Sql.Get("Preferences.Get"), new { UserId = userId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Where(r => Enum.TryParse<CustomerEvent>(r.Event, out _)).ToDictionary(r => Enum.Parse<CustomerEvent>(r.Event), r => new ChannelPreference(r.Email, r.InApp));
    }

    public async Task SetAsync(Guid userId, CustomerEvent kind, ChannelPreference preference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preference);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(Sql.Get("Preferences.Set"), new { UserId = userId, Event = kind.ToString(), preference.Email, preference.InApp }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private sealed class Row
    {
        public string Event { get; init; } = string.Empty;

        public bool Email { get; init; }

        public bool InApp { get; init; }
    }
}
