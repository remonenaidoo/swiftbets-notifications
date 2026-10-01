namespace SwiftBets.Notifications.Application.Ports;

/// <summary>An email provider. False means none is configured; a transient failure throws so the message is redelivered.</summary>
public interface IEmailSender
{
    Task<bool> SendAsync(string to, RenderedEmail email, CancellationToken cancellationToken);
}

public sealed record RenderedEmail(string Subject, string Body);
