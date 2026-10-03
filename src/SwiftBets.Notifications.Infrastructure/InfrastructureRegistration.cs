using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SwiftBets.BuildingBlocks.Core;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.Contracts.Identity;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Contracts.Settlement;
using SwiftBets.Contracts.Payments;
using SwiftBets.Contracts.Compliance;
using SwiftBets.Contracts.Messaging;
using SwiftBets.Contracts.Notifications;
using SwiftBets.Notifications.Application.Ports;
using SwiftBets.Notifications.Infrastructure.Email;
using SwiftBets.Notifications.Infrastructure.Messaging;
using SwiftBets.Notifications.Infrastructure.Persistence;

namespace SwiftBets.Notifications.Infrastructure;

public static class InfrastructureRegistration
{
    public const string SenderGroup = "notifications.sender";
    public const string AccountStatusGroup = "notifications.account-status";

    public static IServiceCollection AddNotificationsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SbNotifications")
            ?? throw new InvalidOperationException("ConnectionStrings:SbNotifications is required.");
        services.AddPostgresPersistence(connectionString);
        services.AddValidatedOptions<EmailOptions>(configuration, EmailOptions.SectionName);
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IDeliveryStore, PostgresDeliveryStore>();
        services.AddSingleton<IAccountStatusStore, PostgresAccountStatusStore>();
        services.AddSingleton<ICustomerInbox, PostgresCustomerInbox>();
        services.AddSingleton<IPreferenceStore, PostgresPreferenceStore>();
        services.AddSingleton<IInboxPublisher, InboxPublisher>();
        services.AddClientCredentials(configuration);
        services.AddHttpClient<IContactDirectory, Identity.IdentityContactDirectory>(http =>
        {
            http.BaseAddress = new Uri((configuration["Clients:IdentityAddress"] ?? "http://identity:8080").TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(5);
        });

        services.AddKafkaMessaging(configuration);
        services.AddKafkaConsumer<NotificationRequestedV1, NotificationRequestedConsumer>(Topics.NotificationRequested, SenderGroup);
        services.AddKafkaConsumer<AccountStatusChangedV1, AccountStatusChangedConsumer>(Topics.AccountStatusChanged, AccountStatusGroup);
        // A new consumer starts at the newest event: customers are told about what happens from now on, never the backlog.
        if (configuration.GetValue("Notifications:CustomerEvents", true))
        {
            services.AddKafkaConsumer<CouponSettledV2, CouponSettledConsumer>(Topics.CouponSettledV2, "notifications.bet-settled", startAtLatest: true);
            services.AddKafkaConsumer<DepositSucceededV1, DepositSucceededConsumer>(Topics.DepositSucceeded, "notifications.deposit-confirmed", startAtLatest: true);
            services.AddKafkaConsumer<LimitReachedV1, LimitReachedConsumer>(Topics.LimitReached, "notifications.limit-reached", startAtLatest: true);
            services.AddKafkaConsumer<SelfExclusionStartedV1, SelfExclusionStartedConsumer>(Topics.SelfExclusionStarted, "notifications.self-exclusion", startAtLatest: true);
        }
        return services;
    }
}
