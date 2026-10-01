using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SwiftBets.BuildingBlocks.Core;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.Contracts.Identity;
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

        services.AddKafkaMessaging(configuration);
        services.AddKafkaConsumer<NotificationRequestedV1, NotificationRequestedConsumer>(Topics.NotificationRequested, SenderGroup);
        services.AddKafkaConsumer<AccountStatusChangedV1, AccountStatusChangedConsumer>(Topics.AccountStatusChanged, AccountStatusGroup);
        return services;
    }
}
