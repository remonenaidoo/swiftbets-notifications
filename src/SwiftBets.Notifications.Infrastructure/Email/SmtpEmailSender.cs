using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using SwiftBets.Notifications.Application.Ports;

namespace SwiftBets.Notifications.Infrastructure.Email;

/// <summary>Sends plain-text email over SMTP: Mailpit in dev and CI, a provider's relay in staging.</summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task<bool> SendAsync(string to, RenderedEmail email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.SmtpHost))
        {
            return false;
        }

        using var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(settings.From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = email.Subject;
        message.Body = new TextPart("plain") { Text = email.Body };

        using var client = new SmtpClient();
        await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, settings.UseTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(settings.Username))
        {
            await client.AuthenticateAsync(settings.Username, settings.Password, cancellationToken).ConfigureAwait(false);
        }

        await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
        await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
