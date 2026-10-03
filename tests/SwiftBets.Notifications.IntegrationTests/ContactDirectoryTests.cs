using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Notifications.Infrastructure.Identity;

namespace SwiftBets.Notifications.IntegrationTests;

public sealed class ContactDirectoryTests
{
    [Fact]
    public async Task A_self_excluded_customer_is_still_written_to_so_their_break_is_confirmed() =>
        (await Directory("SelfExcluded").EmailAsync(Guid.NewGuid(), TestContext.Current.CancellationToken)).ShouldBe("customer@example.com");

    [Fact]
    public async Task A_closed_account_is_not_written_to() =>
        (await Directory("Closed").EmailAsync(Guid.NewGuid(), TestContext.Current.CancellationToken)).ShouldBeNull();

    private static IdentityContactDirectory Directory(string status)
    {
        var handler = new Answer(status);
        var tokens = new ClientCredentialsTokenProvider(new HttpClient(handler), Options.Create(new ClientCredentialsOptions { TokenEndpoint = "http://identity/auth/token", ClientId = "notifications" }), TimeProvider.System);
        return new IdentityContactDirectory(new HttpClient(handler) { BaseAddress = new Uri("http://identity/") }, tokens);
    }

    private sealed class Answer(string status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(request.RequestUri!.AbsolutePath.EndsWith("/token", StringComparison.Ordinal)
                    ? """{"accessToken":"t","expiresIn":600}"""
                    : $$"""{"email":"customer@example.com","emailVerified":true,"status":"{{status}}"}""", Encoding.UTF8, "application/json"),
            });
    }
}
