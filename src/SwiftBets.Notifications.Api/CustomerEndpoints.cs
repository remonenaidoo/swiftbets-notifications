using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Contracts.Errors;
using SwiftBets.Notifications.Application.Ports;
using SwiftBets.Notifications.Domain;

namespace SwiftBets.Notifications.Api;

/// <summary>A signed-in customer's own inbox and notification preferences; every route reads the user from the token.</summary>
public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var me = app.MapGroup("/me").RequireAuthorization();

        me.MapGet("/inbox", async (int? limit, HttpContext context, ICustomerInbox inbox, CancellationToken cancellationToken) =>
            UserId(context) is { } userId
                ? Results.Ok(await inbox.ForUserAsync(userId, Math.Clamp(limit ?? 30, 1, 100), cancellationToken))
                : Unauthenticated(context));

        me.MapPost("/inbox/{messageId:guid}/read", async (Guid messageId, HttpContext context, ICustomerInbox inbox, TimeProvider time, CancellationToken cancellationToken) =>
            UserId(context) is not { } userId ? Unauthenticated(context)
            : await inbox.MarkReadAsync(userId, messageId, time.GetUtcNow(), cancellationToken) ? Results.NoContent()
            : Error.NotFound("message_not_found", "No such message.").ToHttpResult(context));

        me.MapGet("/notification-preferences", async (HttpContext context, IPreferenceStore preferences, CancellationToken cancellationToken) =>
        {
            if (UserId(context) is not { } userId)
            {
                return Unauthenticated(context);
            }

            var chosen = await preferences.GetAsync(userId, cancellationToken);
            return Results.Ok(Enum.GetValues<CustomerEvent>().Select(kind =>
            {
                var channels = CustomerEventPolicy.Channels(kind, chosen.GetValueOrDefault(kind));
                return new PreferenceView(Key(kind), channels.Email, channels.InApp, CustomerEventPolicy.IsMandatory(kind));
            }));
        });

        me.MapPut("/notification-preferences/{kind}", async (string kind, PreferenceBody body, HttpContext context, IPreferenceStore preferences, CancellationToken cancellationToken) =>
        {
            if (UserId(context) is not { } userId)
            {
                return Unauthenticated(context);
            }

            if (Enum.GetValues<CustomerEvent>().FirstOrDefault(k => Key(k) == kind) is not (var parsed and not 0))
            {
                return Error.Validation("unknown_event", "Choose one of bet-settled, deposit-confirmed, limit-reached or self-exclusion.").ToHttpResult(context);
            }

            if (CustomerEventPolicy.IsMandatory(parsed))
            {
                return Error.Validation("preference_locked", "Self-exclusion confirmations are always sent.").ToHttpResult(context);
            }

            await preferences.SetAsync(userId, parsed, new ChannelPreference(body.Email, body.InApp), cancellationToken);
            return Results.NoContent();
        });

        return app;
    }

    private static string Key(CustomerEvent kind) => kind switch
    {
        CustomerEvent.BetSettled => "bet-settled",
        CustomerEvent.DepositConfirmed => "deposit-confirmed",
        CustomerEvent.LimitReached => "limit-reached",
        _ => "self-exclusion",
    };

    private static Guid? UserId(HttpContext context) => Guid.TryParse(context.User.FindFirst("sub")?.Value, out var id) ? id : null;

    private static IResult Unauthenticated(HttpContext context) =>
        new Error("unauthenticated", "Sign in again.", ErrorKind.Unauthorized).ToHttpResult(context);

    public sealed record PreferenceBody(bool Email, bool InApp);

    public sealed record PreferenceView(string Event, bool Email, bool InApp, bool Locked);
}
