using System.ComponentModel.DataAnnotations;

namespace SwiftBets.Notifications.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Empty means no provider: requests are recorded as skipped instead of sent.</summary>
    public string SmtpHost { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 1025;

    public bool UseTls { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    [Required]
    public string From { get; set; } = "SwiftBets <no-reply@swiftbets.local>";
}
