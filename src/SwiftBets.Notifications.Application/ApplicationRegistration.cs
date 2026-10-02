using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SwiftBets.Notifications.Application;

public static class ApplicationRegistration
{
    public static IServiceCollection AddNotificationsApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<SendNotificationHandler>();
        services.AddScoped<CustomerEventHandler>();
        return services;
    }
}
