using SwiftBets.Notifications.Domain;

namespace SwiftBets.Notifications.Application.Ports;

/// <summary>Where to email a customer, asked of identity when a message is sent; null when there is no usable address.</summary>
public interface IContactDirectory
{
    Task<string?> EmailAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record InboxItem(Guid MessageId, Guid UserId, string Category, string Title, string Body, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);

public interface ICustomerInbox
{
    /// <summary>False when the message is already there, so a redelivered event adds nothing.</summary>
    Task<bool> AddAsync(InboxItem item, CancellationToken cancellationToken);

    Task<IReadOnlyList<InboxItem>> ForUserAsync(Guid userId, int limit, CancellationToken cancellationToken);

    Task<bool> MarkReadAsync(Guid userId, Guid messageId, DateTimeOffset readAt, CancellationToken cancellationToken);
}

/// <summary>Pushes a new inbox message to the customer's open sessions.</summary>
public interface IInboxPublisher
{
    Task PublishAsync(InboxItem item, CancellationToken cancellationToken);
}

public interface IPreferenceStore
{
    Task<IReadOnlyDictionary<CustomerEvent, ChannelPreference>> GetAsync(Guid userId, CancellationToken cancellationToken);

    Task SetAsync(Guid userId, CustomerEvent kind, ChannelPreference preference, CancellationToken cancellationToken);
}
