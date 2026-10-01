namespace SwiftBets.Notifications.Domain;

/// <summary>What happened to one notification request; the notification id makes redelivery idempotent.</summary>
public sealed record Delivery(Guid NotificationId, Guid UserId, string Channel, string Template, DeliveryStatus Status, string? Reason, DateTimeOffset RecordedAt);
