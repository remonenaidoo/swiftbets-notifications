using SwiftBets.Notifications.Application.Ports;
using SwiftBets.Notifications.Application.Templates;
using SwiftBets.Notifications.Domain;

namespace SwiftBets.Notifications.Application;

/// <summary>
/// Decides what to do with a request and does it: redelivered requests are ignored, other channels are skipped,
/// marketing to a restricted account is suppressed, unrenderable requests are rejected, and the rest are emailed.
/// A send failure throws and records nothing, so the message is retried.
/// </summary>
public sealed class SendNotificationHandler(IDeliveryStore deliveries, IAccountStatusStore statuses, IEmailSender email, TimeProvider time)
{
    public const string EmailChannel = "Email";

    public async Task<DeliveryStatus?> HandleAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await deliveries.ExistsAsync(request.NotificationId, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        if (!string.Equals(request.Channel, EmailChannel, StringComparison.Ordinal))
        {
            return await RecordAsync(request, DeliveryStatus.Skipped, $"channel {request.Channel} is not delivered yet", cancellationToken).ConfigureAwait(false);
        }

        if (request.IsMarketing && !MarketingPolicy.MayReceiveMarketing(await statuses.GetAsync(request.UserId, cancellationToken).ConfigureAwait(false)))
        {
            return await RecordAsync(request, DeliveryStatus.Suppressed, "account may not receive marketing", cancellationToken).ConfigureAwait(false);
        }

        var (rendered, problem) = EmailTemplates.Render(request.Template, request.Data);
        if (rendered is null)
        {
            return await RecordAsync(request, DeliveryStatus.Rejected, problem, cancellationToken).ConfigureAwait(false);
        }

        if (!IsAddress(request.Recipient))
        {
            return await RecordAsync(request, DeliveryStatus.Rejected, "recipient is not an email address", cancellationToken).ConfigureAwait(false);
        }

        return await email.SendAsync(request.Recipient, rendered, cancellationToken).ConfigureAwait(false)
            ? await RecordAsync(request, DeliveryStatus.Sent, null, cancellationToken).ConfigureAwait(false)
            : await RecordAsync(request, DeliveryStatus.Skipped, "no email provider configured", cancellationToken).ConfigureAwait(false);
    }

    private static bool IsAddress(string recipient)
    {
        var at = recipient.IndexOf('@', StringComparison.Ordinal);
        return at > 0 && at < recipient.Length - 1 && !recipient.Any(char.IsWhiteSpace);
    }

    private async Task<DeliveryStatus> RecordAsync(NotificationRequest request, DeliveryStatus status, string? reason, CancellationToken cancellationToken)
    {
        await deliveries.RecordAsync(
            new Delivery(request.NotificationId, request.UserId, request.Channel, request.Template, status, reason, time.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
        return status;
    }
}
