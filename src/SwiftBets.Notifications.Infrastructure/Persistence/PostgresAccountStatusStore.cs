using Dapper;
using Npgsql;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.Notifications.Application.Ports;

namespace SwiftBets.Notifications.Infrastructure.Persistence;

public sealed class PostgresAccountStatusStore(NpgsqlDataSource dataSource) : IAccountStatusStore
{
    private static readonly SqlResources Sql = SqlResources.For<PostgresAccountStatusStore>();

    public async Task<string?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(Sql.Get("AccountStatus.Get"), new { UserId = userId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task SetAsync(Guid userId, string status, DateTimeOffset changedAt, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(Sql.Get("AccountStatus.Set"), new { UserId = userId, Status = status, ChangedAt = changedAt }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
