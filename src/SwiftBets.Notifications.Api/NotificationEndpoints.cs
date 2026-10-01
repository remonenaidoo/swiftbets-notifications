using SwiftBets.Notifications.Application.Ports;

namespace SwiftBets.Notifications.Api;

public static class NotificationEndpoints
{
    public const string ReadPermission = "notifications.deliveries.read";

    /// <summary>Staff see what was sent to a customer and why anything was not; message bodies are never stored.</summary>
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/users/{userId:guid}/notifications", async (Guid userId, int? limit, IDeliveryStore deliveries, CancellationToken cancellationToken) =>
            Results.Ok(await deliveries.ForUserAsync(userId, Math.Clamp(limit ?? 50, 1, 200), cancellationToken)))
            .RequireAuthorization(ReadPermission);
        return app;
    }
}
