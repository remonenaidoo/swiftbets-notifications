namespace SwiftBets.Notifications.Domain;

/// <summary>Things that happen to a customer's account that they are told about.</summary>
public enum CustomerEvent
{
    BetSettled = 1,
    DepositConfirmed = 2,
    LimitReached = 3,
    SelfExclusionConfirmed = 4,
}

/// <summary>Where a customer wants to hear about one kind of event.</summary>
public sealed record ChannelPreference(bool Email, bool InApp)
{
    public static ChannelPreference Default { get; } = new(true, true);
}

public static class CustomerEventPolicy
{
    /// <summary>A self-exclusion is always confirmed on every channel: the customer must know it took effect.</summary>
    public static bool IsMandatory(CustomerEvent kind) => kind == CustomerEvent.SelfExclusionConfirmed;

    public static ChannelPreference Channels(CustomerEvent kind, ChannelPreference? chosen) =>
        IsMandatory(kind) ? ChannelPreference.Default : chosen ?? ChannelPreference.Default;
}
