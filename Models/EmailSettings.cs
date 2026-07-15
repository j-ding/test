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
    // Shared mailboxes users can optionally send a notification "as" instead of their own
    // identity (e.g. a shared "IT Operations" alias). Purely a configured allow-list — the app
    // doesn't verify real Exchange Send-As delegation, since Mail.Send (Application permission)
    // can already send as any mailbox in the tenant regardless of who's picked here.
    public List<SharedMailbox> SharedMailboxes { get; set; } = [];
}

public class SharedMailbox
{
    public string DisplayName { get; set; } = "";
    public string Address { get; set; } = "";
}
