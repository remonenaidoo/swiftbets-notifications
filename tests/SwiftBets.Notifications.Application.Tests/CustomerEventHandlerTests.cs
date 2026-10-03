using Microsoft.Extensions.Time.Testing;
using SwiftBets.Notifications.Application.Ports;
using SwiftBets.Notifications.Domain;

namespace SwiftBets.Notifications.Application.Tests;

public sealed class CustomerEventHandlerTests
{
    private static readonly Guid Customer = Guid.NewGuid();
    private static readonly Dictionary<string, string> Won = new() { ["outcome"] = "Won", ["stake"] = "R 10,00", ["payout"] = "R 25,00" };

    [Fact]
    public async Task A_settled_bet_reaches_the_inbox_and_the_email_once_however_often_it_is_delivered()
    {
        var (handler, inbox, email, _) = Build();

        var settledAt = new DateTimeOffset(2026, 10, 1, 18, 30, 0, TimeSpan.Zero);
        await handler.HandleAsync(CustomerEvent.BetSettled, "settled|c-1|1", Customer, Won, settledAt, CancellationToken.None);
        await handler.HandleAsync(CustomerEvent.BetSettled, "settled|c-1|1", Customer, Won, settledAt, CancellationToken.None);

        var item = inbox.Items.ShouldHaveSingleItem();
        (item.Title, item.CreatedAt).ShouldBe(("Your bet won", settledAt));
        email.Sent.ShouldHaveSingleItem().To.ShouldBe("punter@example.com");
    }

    [Fact]
    public async Task A_customer_who_turned_bet_emails_off_gets_the_inbox_message_only_but_a_break_is_always_emailed()
    {
        var (handler, inbox, email, preferences) = Build();
        await preferences.SetAsync(Customer, CustomerEvent.BetSettled, new ChannelPreference(Email: false, InApp: true), CancellationToken.None);
        await preferences.SetAsync(Customer, CustomerEvent.SelfExclusionConfirmed, new ChannelPreference(Email: false, InApp: false), CancellationToken.None);

        await handler.HandleAsync(CustomerEvent.BetSettled, "settled|c-2|1", Customer, Won, DateTimeOffset.UtcNow, CancellationToken.None);
        email.Sent.ShouldBeEmpty();

        await handler.HandleAsync(CustomerEvent.SelfExclusionConfirmed, "exclusion|x", Customer, new Dictionary<string, string> { ["kind"] = "self-exclusion" }, DateTimeOffset.UtcNow, CancellationToken.None);
        email.Sent.ShouldHaveSingleItem().Email.Subject.ShouldBe("Your break has started");
        inbox.Items.Count.ShouldBe(2);
    }

    private static (CustomerEventHandler, MemoryInbox, RecordingEmail, MemoryPreferences) Build()
    {
        var inbox = new MemoryInbox();
        var email = new RecordingEmail();
        var preferences = new MemoryPreferences();
        var handler = new CustomerEventHandler(inbox, new NullPublisher(), preferences, new FixedContacts(), new InMemoryDeliveries(), email, new FakeTimeProvider(DateTimeOffset.UtcNow));
        return (handler, inbox, email, preferences);
    }

    private sealed class MemoryInbox : ICustomerInbox
    {
        public List<InboxItem> Items { get; } = [];

        public Task<bool> AddAsync(InboxItem item, CancellationToken cancellationToken)
        {
            if (Items.Any(i => i.MessageId == item.MessageId))
            {
                return Task.FromResult(false);
            }

            Items.Add(item);
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<InboxItem>> ForUserAsync(Guid userId, int limit, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<InboxItem>>(Items);

        public Task<bool> MarkReadAsync(Guid userId, Guid messageId, DateTimeOffset readAt, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class MemoryPreferences : IPreferenceStore
    {
        private readonly Dictionary<CustomerEvent, ChannelPreference> _rows = [];

        public Task<IReadOnlyDictionary<CustomerEvent, ChannelPreference>> GetAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<CustomerEvent, ChannelPreference>>(_rows);

        public Task SetAsync(Guid userId, CustomerEvent kind, ChannelPreference preference, CancellationToken cancellationToken)
        {
            _rows[kind] = preference;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedContacts : IContactDirectory
    {
        public Task<string?> EmailAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<string?>("punter@example.com");
    }

    private sealed class NullPublisher : IInboxPublisher
    {
        public Task PublishAsync(InboxItem item, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
