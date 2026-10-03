using System.Globalization;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Contracts.Compliance;
using SwiftBets.Contracts.Money;
using SwiftBets.Contracts.Payments;
using SwiftBets.Contracts.Settlement;
using SwiftBets.Notifications.Application;
using SwiftBets.Notifications.Domain;

namespace SwiftBets.Notifications.Infrastructure.Messaging;

internal static class Format
{
    private static readonly CultureInfo SouthAfrica = CultureInfo.GetCultureInfo("en-ZA");

    public static string Rand(Money money) => (money.MinorUnits / 100m).ToString("C", SouthAfrica);
}

/// <summary>Each settlement version is one message: a resettled bet tells the customer again, a redelivery does not.</summary>
public sealed class CouponSettledConsumer(CustomerEventHandler handler) : IEventHandler<CouponSettledV2>
{
    public Task HandleAsync(ConsumedEvent<CouponSettledV2> message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var p = message.Envelope.Payload;
        return handler.HandleAsync(CustomerEvent.BetSettled, $"settled|{p.CouponId}|{p.SettlementVersion}", p.PunterId,
            new Dictionary<string, string> { ["outcome"] = p.Outcome.ToString(), ["stake"] = Format.Rand(p.TotalStake), ["payout"] = Format.Rand(p.TargetPayout) }, p.SettledAt, cancellationToken);
    }
}

public sealed class DepositSucceededConsumer(CustomerEventHandler handler) : IEventHandler<DepositSucceededV1>
{
    public Task HandleAsync(ConsumedEvent<DepositSucceededV1> message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var p = message.Envelope.Payload;
        return handler.HandleAsync(CustomerEvent.DepositConfirmed, $"deposit|{p.PaymentId}", p.UserId, new Dictionary<string, string> { ["amount"] = Format.Rand(p.Amount) }, p.CompletedAt, cancellationToken);
    }
}

/// <summary>At most one notice per limit per day: a customer pressing against a limit is told once, not on every refusal.</summary>
public sealed class LimitReachedConsumer(CustomerEventHandler handler) : IEventHandler<LimitReachedV1>
{
    public Task HandleAsync(ConsumedEvent<LimitReachedV1> message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var p = message.Envelope.Payload;
        return handler.HandleAsync(CustomerEvent.LimitReached, $"limit|{p.UserId}|{p.Kind}|{p.Period}|{p.ReachedAt:yyyy-MM-dd}", p.UserId, new Dictionary<string, string>
        {
            ["kind"] = p.Kind.ToString().ToLowerInvariant(),
            ["period"] = p.Period switch { LimitPeriod.Day => "daily", LimitPeriod.Week => "weekly", _ => "monthly" },
            ["refused"] = p.Refused,
            ["attempted"] = Format.Rand(p.Attempted),
        }, p.ReachedAt, cancellationToken);
    }
}

public sealed class SelfExclusionStartedConsumer(CustomerEventHandler handler) : IEventHandler<SelfExclusionStartedV1>
{
    public Task HandleAsync(ConsumedEvent<SelfExclusionStartedV1> message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var p = message.Envelope.Payload;
        return handler.HandleAsync(CustomerEvent.SelfExclusionConfirmed, $"exclusion|{p.UserId}|{p.StartsAt:O}", p.UserId, new Dictionary<string, string>
        {
            ["kind"] = p.Kind == RestrictionKind.CoolingOff ? "cooling-off break" : "self-exclusion",
            ["until"] = p.EndsAt?.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("en-ZA")) ?? string.Empty,
        }, p.StartsAt, cancellationToken);
    }
}
