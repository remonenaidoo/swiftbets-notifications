namespace SwiftBets.Notifications.Domain;

/// <summary>Only active accounts receive marketing; service messages (verification, reset, receipts) always go.</summary>
public static class MarketingPolicy
{
    public const string ActiveStatus = "Active";

    public static bool MayReceiveMarketing(string? accountStatus) =>
        accountStatus is null || string.Equals(accountStatus, ActiveStatus, StringComparison.Ordinal);
}
