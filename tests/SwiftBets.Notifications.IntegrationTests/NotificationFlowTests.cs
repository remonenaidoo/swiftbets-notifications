extern alias migrator;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dapper;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Polly.Registry;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.Contracts.Identity;
using SwiftBets.Contracts.Messaging;
using SwiftBets.Contracts.Notifications;

namespace SwiftBets.Notifications.IntegrationTests;

public sealed class NotificationFlowTests(PostgresFixture postgres, RedpandaFixture redpanda)
{
    [Fact]
    public async Task A_requested_verification_email_is_sent_once_even_when_redelivered()
    {
        await using var smtp = new FakeSmtpServer();
        await using var host = await NotificationsHost.StartAsync(postgres, redpanda, smtp.Port);
        using var publisher = Publisher(host);
        var request = Verification("new.punter@example.com");

        await publisher.PublishAsync(Topics.NotificationRequested, request.UserId.ToString(), Envelope(request), CancellationToken.None);
        await publisher.PublishAsync(Topics.NotificationRequested, request.UserId.ToString(), Envelope(request), CancellationToken.None);

        await WaitUntilAsync(() => !smtp.Messages.IsEmpty);
        await WaitUntilAsync(async () => await StatusAsync(host, request.NotificationId) == 0);
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        smtp.Messages.Count.ShouldBe(1);
        smtp.Messages.TryPeek(out var message).ShouldBeTrue();
        message.To.ShouldBe("new.punter@example.com");
        message.Data.ShouldContain("Subject: Confirm your SwiftBets email address");
        message.Data.ShouldContain("https://swiftbets.test/account/verify?token=abc");
    }

    [Fact]
    public async Task Marketing_to_a_self_excluded_customer_is_suppressed_and_staff_can_see_why()
    {
        await using var smtp = new FakeSmtpServer();
        await using var host = await NotificationsHost.StartAsync(postgres, redpanda, smtp.Port);
        using var publisher = Publisher(host);
        var userId = Guid.NewGuid();
        var excluded = new AccountStatusChangedV1(userId, AccountStatus.Active, AccountStatus.SelfExcluded, "customer request", userId.ToString(), DateTimeOffset.UtcNow);
        await publisher.PublishAsync(Topics.AccountStatusChanged, userId.ToString(), EventEnvelope<AccountStatusChangedV1>.Create(excluded, DateTimeOffset.UtcNow, "corr-status"), CancellationToken.None);
        await WaitUntilAsync(async () => await AccountStatusAsync(host, userId) == "SelfExcluded");

        var promo = Verification("excluded@example.com", userId) with { IsMarketing = true };
        await publisher.PublishAsync(Topics.NotificationRequested, userId.ToString(), Envelope(promo), CancellationToken.None);
        await WaitUntilAsync(async () => await StatusAsync(host, promo.NotificationId) is not null);

        (await StatusAsync(host, promo.NotificationId)).ShouldBe((short)1);
        smtp.Messages.ShouldBeEmpty();

        using var client = host.CreateClient();
        (await client.GetAsync($"/admin/users/{userId}/notifications", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", NotificationsHost.StaffToken("identity.users.read"));
        (await client.GetAsync($"/admin/users/{userId}/notifications", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", NotificationsHost.StaffToken("notifications.deliveries.read"));
        var deliveries = await client.GetFromJsonAsync<List<DeliveryView>>($"/admin/users/{userId}/notifications", TestContext.Current.CancellationToken);
        deliveries!.Single().Reason.ShouldBe("account may not receive marketing");
    }

    [Fact]
    public async Task The_migration_rolls_back_and_reapplies()
    {
        await using var smtp = new FakeSmtpServer();
        await using var host = await NotificationsHost.StartAsync(postgres, redpanda, smtp.Port);

        (await NotificationsHost.MigrateAsync(host.ConnectionString, "--Migrator:RollbackTo=0")).ShouldBe(0);
        (await TablesAsync(host)).ShouldBe(0);
        (await NotificationsHost.MigrateAsync(host.ConnectionString)).ShouldBe(0);
        (await TablesAsync(host)).ShouldBe(2);
    }

    [Fact]
    public async Task Missing_connection_string_fails_with_a_usage_code()
    {
        var entryPoint = typeof(migrator::Program).Assembly.EntryPoint!;
        var result = entryPoint.Invoke(null, [Array.Empty<string>()]);
        (result is Task<int> task ? await task : (int)result!).ShouldBe(2);
    }

    private sealed record DeliveryView(Guid NotificationId, string Template, int Status, string? Reason);

    private static NotificationRequestedV1 Verification(string to, Guid? userId = null) => new(
        Guid.NewGuid(), userId ?? Guid.NewGuid(), NotificationChannel.Email, "account.verify-email", to,
        new Dictionary<string, string> { ["link"] = "https://swiftbets.test/account/verify?token=abc" }, false, DateTimeOffset.UtcNow);

    private static EventEnvelope<NotificationRequestedV1> Envelope(NotificationRequestedV1 request) =>
        EventEnvelope<NotificationRequestedV1>.Create(request, DateTimeOffset.UtcNow, "corr-notify");

    private static KafkaEventPublisher Publisher(NotificationsHost host) => new(
        Options.Create(new KafkaOptions { BootstrapServers = host.Bootstrap, Environment = host.Environment, ClientId = "notifications-tests" }),
        host.Services.GetRequiredService<ResiliencePipelineProvider<string>>());

    private static async Task<short?> StatusAsync(NotificationsHost host, Guid notificationId)
    {
        await using var connection = new NpgsqlConnection(host.ConnectionString);
        return await connection.ExecuteScalarAsync<short?>("SELECT status FROM notifications.deliveries WHERE notification_id = @id", new { id = notificationId });
    }

    private static async Task<string?> AccountStatusAsync(NotificationsHost host, Guid userId)
    {
        await using var connection = new NpgsqlConnection(host.ConnectionString);
        return await connection.ExecuteScalarAsync<string?>("SELECT status FROM notifications.account_status WHERE user_id = @id", new { id = userId });
    }

    private static async Task<int> TablesAsync(NotificationsHost host)
    {
        await using var connection = new NpgsqlConnection(host.ConnectionString);
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'notifications'");
    }

    private static Task WaitUntilAsync(Func<bool> condition) => WaitUntilAsync(() => Task.FromResult(condition()));

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (!await condition())
        {
            DateTimeOffset.UtcNow.ShouldBeLessThan(deadline, "the condition never became true");
            await Task.Delay(200, TestContext.Current.CancellationToken);
        }
    }
}
