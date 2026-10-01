namespace SwiftBets.Notifications.Domain;

public enum DeliveryStatus
{
    Sent = 0,

    /// <summary>A marketing message to a customer who may not receive marketing.</summary>
    Suppressed = 1,

    /// <summary>A channel this service does not deliver yet.</summary>
    Skipped = 2,

    /// <summary>The request can never be delivered (unknown template, missing data, bad address).</summary>
    Rejected = 3,
}
