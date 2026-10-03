using SwiftBets.Notifications.Domain;

namespace SwiftBets.Notifications.Application.Templates;

/// <summary>The title and text for each customer event, shared by the email and the in-app inbox.</summary>
public static class CustomerMessages
{
    public static (string Title, string Body) Render(CustomerEvent kind, IReadOnlyDictionary<string, string> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        string Get(string key) => data.TryGetValue(key, out var value) ? value : string.Empty;
        return kind switch
        {
            CustomerEvent.BetSettled => Get("outcome") switch
            {
                "Won" => ("Your bet won", $"Your {Get("stake")} bet won {Get("payout")}. It has been paid to your balance."),
                "Void" => ("Your bet was made void", $"Your {Get("stake")} bet was made void and the stake has been returned to your balance."),
                "CashedOut" => ("Your bet was cashed out", $"Your {Get("stake")} bet was cashed out for {Get("payout")}."),
                _ => ("Your bet has settled", $"Your {Get("stake")} bet did not win this time."),
            },
            CustomerEvent.DepositConfirmed => ("Deposit received", $"{Get("amount")} has been added to your balance."),
            CustomerEvent.LimitReached => ("You reached one of your limits",
                $"A {Get("refused")} of {Get("attempted")} was not taken because it would pass your {Get("period")} {Get("kind")} limit. You can review your limits under Safer gambling."),
            CustomerEvent.SelfExclusionConfirmed => ("Your break has started",
                $"Your {Get("kind")} has started{(Get("until") is { Length: > 0 } until ? $" and lasts until {until}" : string.Empty)}. You cannot bet or deposit until it ends. If you need support, help is available from the South African Responsible Gambling Foundation on 0800 006 008."),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No message for this event."),
        };
    }
}
