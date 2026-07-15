namespace SFSWebForm.Models;

// Named MailSettings to avoid collision with Microsoft.Graph.Models.EmailSettings
public class MailSettings
{
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    // Fallback sender mailbox used only if the signed-in user has no email claim — normal sends
    // go out as whoever is actually signed in (e.g. "incidentbot@yourcompany.com" as a last resort).
    public string SenderMailbox { get; set; } = "";
    // Comma-separated fallback when an incident has no Recipients set
    public string DefaultRecipients { get; set; } = "";
}
