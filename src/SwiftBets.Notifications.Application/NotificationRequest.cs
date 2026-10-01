namespace SwiftBets.Notifications.Application;

/// <summary>One request to notify a customer, as this service sees it (mapped from the Kafka contract).</summary>
public sealed record NotificationRequest(
    Guid NotificationId,
    Guid UserId,
    string Channel,
    string Template,
    string Recipient,
    IReadOnlyDictionary<string, string> Data,
    bool IsMarketing);
