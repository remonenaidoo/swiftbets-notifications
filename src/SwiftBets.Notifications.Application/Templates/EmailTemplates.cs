namespace SwiftBets.Notifications.Application.Templates;

/// <summary>
/// Email templates by name. Each lists the data keys it needs; a request missing one is rejected rather than sent with
/// a hole in it. Links arrive ready-made because the sending service owns its URLs.
/// </summary>
public static class EmailTemplates
{
    public const string VerifyEmail = "account.verify-email";
    public const string ResetPassword = "account.reset-password";
    public const string AlreadyRegistered = "account.already-registered";

    private static readonly Dictionary<string, (string[] Keys, Func<IReadOnlyDictionary<string, string>, (string Subject, string Body)> Render)> Catalogue =
        new(StringComparer.Ordinal)
        {
            [VerifyEmail] = (["link"], d => (
                "Confirm your SwiftBets email address",
                $"Welcome to SwiftBets.\n\nConfirm your email address within 24 hours:\n{d["link"]}\n\nIf you did not open an account, ignore this email.")),
            [ResetPassword] = (["link"], d => (
                "Reset your SwiftBets password",
                $"Someone asked to reset the password for this address.\n\nChoose a new password within one hour:\n{d["link"]}\n\nIf it was not you, ignore this email; your password is unchanged.")),
            [AlreadyRegistered] = (["signInLink", "resetLink"], d => (
                "You already have a SwiftBets account",
                $"Someone tried to open a SwiftBets account with this address, which already has one.\n\nSign in: {d["signInLink"]}\nForgot your password? {d["resetLink"]}\n\nIf it was not you, no action is needed.")),
        };

    public static IReadOnlyCollection<string> Names => Catalogue.Keys;

    /// <summary>The rendered email, or the reason it cannot be rendered.</summary>
    public static (Ports.RenderedEmail? Email, string? Problem) Render(string template, IReadOnlyDictionary<string, string> data)
    {
        if (!Catalogue.TryGetValue(template, out var entry))
        {
            return (null, $"unknown template '{template}'");
        }

        var missing = entry.Keys.Where(k => !data.TryGetValue(k, out var v) || string.IsNullOrWhiteSpace(v)).ToList();
        if (missing.Count > 0)
        {
            return (null, $"template '{template}' needs {string.Join(", ", missing)}");
        }

        var (subject, body) = entry.Render(data);
        return (new Ports.RenderedEmail(subject, body), null);
    }
}
