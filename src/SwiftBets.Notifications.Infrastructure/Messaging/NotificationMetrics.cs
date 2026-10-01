using Prometheus;

namespace SwiftBets.Notifications.Infrastructure.Messaging;

internal static class NotificationMetrics
{
    public static readonly Counter Deliveries = Metrics.CreateCounter(
        "swiftbets_notifications_deliveries_total",
        "Notification requests handled, by channel, template and outcome.",
        new CounterConfiguration { LabelNames = ["channel", "template", "status"] });
}
