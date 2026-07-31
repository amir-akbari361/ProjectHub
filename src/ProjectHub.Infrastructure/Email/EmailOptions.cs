using System.ComponentModel.DataAnnotations;

namespace ProjectHub.Infrastructure.Email;

/// <summary>
/// Strongly-typed SMTP configuration, bound from the "Email" section via the Options pattern and
/// validated at startup. Keeping these as validated options (not raw <c>IConfiguration</c> reads)
/// means a deployment missing the SMTP host or sender address fails at boot, not when the first user
/// tries to confirm their email — a far cheaper failure to diagnose.
/// </summary>
internal sealed class EmailOptions
{
    /// <summary>The configuration section name — the single source of the "Email" magic string.</summary>
    public const string SectionName = "Email";

    /// <summary>SMTP server hostname (e.g. "smtp.sendgrid.net" or a local MailHog in dev).</summary>
    [Required]
    public string Host { get; init; } = string.Empty;

    /// <summary>SMTP port. 587 (STARTTLS) is the modern submission standard; 25 is legacy/relay.</summary>
    [Range(1, 65535)]
    public int Port { get; init; } = 587;

    /// <summary>Whether to negotiate TLS. Should be true in every real environment.</summary>
    public bool UseSsl { get; init; } = true;

    /// <summary>SMTP auth username. For providers like SendGrid this is often a literal "apikey".</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>SMTP auth password / API key. Sourced from a secret store in production, never committed.</summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>The "From" address recipients see. Must be a verified sender for the SMTP provider.</summary>
    [Required]
    [EmailAddress]
    public string FromAddress { get; init; } = string.Empty;

    /// <summary>The friendly "From" display name (e.g. "ProjectHub").</summary>
    [Required]
    public string FromName { get; init; } = string.Empty;
}
