using Microsoft.Extensions.Time.Testing;
using SwiftBets.Notifications.Application.Templates;
using SwiftBets.Notifications.Domain;

namespace SwiftBets.Notifications.Application.Tests;

public sealed class SendNotificationHandlerTests
{
    private readonly InMemoryDeliveries _deliveries = new();
    private readonly InMemoryStatuses _statuses = new();

    private static NotificationRequest Verify(string channel = "Email", string recipient = "punter@example.com", bool marketing = false, Dictionary<string, string>? data = null, string template = EmailTemplates.VerifyEmail) =>
        new(Guid.NewGuid(), Guid.NewGuid(), channel, template, recipient, data ?? new() { ["link"] = "https://swiftbets.test/account/verify?token=abc" }, marketing);

    private SendNotificationHandler Handler(RecordingEmail email) => new(_deliveries, _statuses, email, new FakeTimeProvider(new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero)));

    [Fact]
    public async Task A_service_email_is_sent_once_and_recorded()
    {
        var email = new RecordingEmail();
        var request = Verify();

        (await Handler(email).HandleAsync(request, CancellationToken.None)).ShouldBe(DeliveryStatus.Sent);
        (await Handler(email).HandleAsync(request, CancellationToken.None)).ShouldBeNull();

        email.Sent.Count.ShouldBe(1);
        email.Sent[0].To.ShouldBe("punter@example.com");
        email.Sent[0].Email.Body.ShouldContain("https://swiftbets.test/account/verify?token=abc");
        _deliveries.Rows[request.NotificationId].Status.ShouldBe(DeliveryStatus.Sent);
    }

    [Fact]
    public async Task A_send_failure_records_nothing_so_the_retry_sends_it()
    {
        var email = new RecordingEmail(failures: 1);
        var request = Verify();

        await Should.ThrowAsync<IOException>(() => Handler(email).HandleAsync(request, CancellationToken.None));
        _deliveries.Rows.ShouldBeEmpty();

        (await Handler(email).HandleAsync(request, CancellationToken.None)).ShouldBe(DeliveryStatus.Sent);
        email.Sent.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("SelfExcluded", DeliveryStatus.Suppressed)]
    [InlineData("Suspended", DeliveryStatus.Suppressed)]
    [InlineData("Closed", DeliveryStatus.Suppressed)]
    [InlineData("Active", DeliveryStatus.Sent)]
    [InlineData(null, DeliveryStatus.Sent)]
    public async Task Marketing_goes_only_to_active_accounts(string? status, DeliveryStatus expected)
    {
        var email = new RecordingEmail();
        var request = Verify(marketing: true);
        if (status is not null)
        {
            _statuses.Rows[request.UserId] = status;
        }

        (await Handler(email).HandleAsync(request, CancellationToken.None)).ShouldBe(expected);
        email.Sent.Count.ShouldBe(expected == DeliveryStatus.Sent ? 1 : 0);
    }

    [Fact]
    public async Task Service_messages_reach_a_self_excluded_customer()
    {
        var email = new RecordingEmail();
        var request = Verify(template: EmailTemplates.ResetPassword);
        _statuses.Rows[request.UserId] = "SelfExcluded";

        (await Handler(email).HandleAsync(request, CancellationToken.None)).ShouldBe(DeliveryStatus.Sent);
    }

    [Fact]
    public async Task Unknown_templates_missing_data_and_bad_addresses_are_rejected_without_sending()
    {
        var email = new RecordingEmail();
        var unknown = Verify(template: "promo.free-bet");
        var noLink = Verify(data: []);
        var badAddress = Verify(recipient: "not an address");

        (await Handler(email).HandleAsync(unknown, CancellationToken.None)).ShouldBe(DeliveryStatus.Rejected);
        (await Handler(email).HandleAsync(noLink, CancellationToken.None)).ShouldBe(DeliveryStatus.Rejected);
        (await Handler(email).HandleAsync(badAddress, CancellationToken.None)).ShouldBe(DeliveryStatus.Rejected);

        email.Sent.ShouldBeEmpty();
        _deliveries.Rows[unknown.NotificationId].Reason.ShouldBe("unknown template 'promo.free-bet'");
        _deliveries.Rows[noLink.NotificationId].Reason.ShouldBe("template 'account.verify-email' needs link");
    }

    [Fact]
    public async Task Other_channels_are_skipped_until_they_are_built()
    {
        (await Handler(new RecordingEmail()).HandleAsync(Verify(channel: "Push"), CancellationToken.None)).ShouldBe(DeliveryStatus.Skipped);
    }

    [Fact]
    public async Task With_no_provider_configured_the_request_is_skipped_not_marked_sent()
    {
        var request = Verify();

        (await Handler(new RecordingEmail(configured: false)).HandleAsync(request, CancellationToken.None)).ShouldBe(DeliveryStatus.Skipped);
        _deliveries.Rows[request.NotificationId].Reason.ShouldBe("no email provider configured");
    }

    [Fact]
    public void Every_template_renders_with_its_keys()
    {
        foreach (var name in EmailTemplates.Names)
        {
            var data = new Dictionary<string, string> { ["link"] = "l", ["signInLink"] = "s", ["resetLink"] = "r" };
            var (email, problem) = EmailTemplates.Render(name, data);
            problem.ShouldBeNull();
            email!.Subject.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("Active", true)]
    [InlineData("active", false)]
    [InlineData("SelfExcluded", false)]
    public void Marketing_policy(string? status, bool allowed) => MarketingPolicy.MayReceiveMarketing(status).ShouldBe(allowed);
}
