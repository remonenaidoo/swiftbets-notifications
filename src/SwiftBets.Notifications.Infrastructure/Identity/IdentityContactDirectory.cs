using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Notifications.Application.Ports;

namespace SwiftBets.Notifications.Infrastructure.Identity;

/// <summary>
/// Asks identity where to email a customer, with a service token; nothing about the address is kept here. A suspended or
/// self-excluded account is still written to: these are service messages, and a break must be confirmed. Marketing is
/// filtered separately. Only a closed account is not.
/// </summary>
public sealed class IdentityContactDirectory(HttpClient http, ClientCredentialsTokenProvider tokens) : IContactDirectory
{
    public async Task<string?> EmailAsync(Guid userId, CancellationToken cancellationToken)
    {
        var token = await tokens.GetTokenAsync(cancellationToken).ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"internal/users/{userId}/contact");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            tokens.Invalidate(token);
        }

        response.EnsureSuccessStatusCode();
        var contact = await response.Content.ReadFromJsonAsync<Contact>(cancellationToken).ConfigureAwait(false);
        return contact?.Email is { Length: > 0 } email && contact.Status != "Closed" ? email : null;
    }

    private sealed record Contact(string? Email, bool EmailVerified, string Status);
}
