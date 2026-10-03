using System.Security.Cryptography;
using System.Text;
using SwiftBets.Notifications.Application.Ports;
using SwiftBets.Notifications.Application.Templates;
using SwiftBets.Notifications.Domain;

namespace SwiftBets.Notifications.Application;

/// <summary>
/// Tells a customer about an event on the channels they chose (always both for a self-exclusion). The inbox message
/// and the email each take an id derived from the event, so a redelivered event adds and sends nothing more. An email
/// failure throws, so the event is redelivered and only the missing part is done again.
/// </summary>
public sealed class CustomerEventHandler(ICustomerInbox inbox, IInboxPublisher publisher, IPreferenceStore preferences, IContactDirectory contacts, IDeliveryStore deliveries, IEmailSender email, TimeProvider time)
{
    public async Task HandleAsync(CustomerEvent kind, string eventKey, Guid userId, IReadOnlyDictionary<string, string> data, CancellationToken cancellationToken)
    {
        var chosen = (await preferences.GetAsync(userId, cancellationToken).ConfigureAwait(false)).GetValueOrDefault(kind);
        var channels = CustomerEventPolicy.Channels(kind, chosen);
        var (title, body) = CustomerMessages.Render(kind, data);
        var now = time.GetUtcNow();

        if (channels.InApp)
        {
            var item = new InboxItem(DerivedId(eventKey, "inbox"), userId, Category(kind), title, body, now, null);
            if (await inbox.AddAsync(item, cancellationToken).ConfigureAwait(false))
            {
                await publisher.PublishAsync(item, cancellationToken).ConfigureAwait(false);
            }
        }

        var emailId = DerivedId(eventKey, "email");
        if (!channels.Email || await deliveries.ExistsAsync(emailId, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var template = $"customer.{Category(kind)}";
        var address = await contacts.EmailAsync(userId, cancellationToken).ConfigureAwait(false);
        var status = address is null ? DeliveryStatus.Rejected
            : await email.SendAsync(address, new RenderedEmail(title, $"{body}\n\nSwiftBets. 18+ only. Play responsibly."), cancellationToken).ConfigureAwait(false) ? DeliveryStatus.Sent
            : DeliveryStatus.Skipped;
        await deliveries.RecordAsync(new Delivery(emailId, userId, SendNotificationHandler.EmailChannel, template, status,
            status switch { DeliveryStatus.Rejected => "no email address on the account", DeliveryStatus.Skipped => "no email provider configured", _ => null }, now), cancellationToken).ConfigureAwait(false);
    }

    private static string Category(CustomerEvent kind) => kind switch
    {
        CustomerEvent.BetSettled => "bet-settled",
        CustomerEvent.DepositConfirmed => "deposit-confirmed",
        CustomerEvent.LimitReached => "limit-reached",
        _ => "self-exclusion",
    };

    /// <summary>A stable id per event and channel, so every redelivery of the event maps to the same message.</summary>
    private static Guid DerivedId(string eventKey, string channel) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"{eventKey}|{channel}")).AsSpan(0, 16));
}
