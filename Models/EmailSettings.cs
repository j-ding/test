namespace SFSWebForm.Models;

// Named MailSettings to avoid collision with Microsoft.Graph.Models.EmailSettings
public class MailSettings
{
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    // Combined with the Windows username to derive the sender address (e.g. "yourcompany.com")
    public string EmailDomain { get; set; } = "";
    // Explicit sender mailbox for Graph Mail.Send (e.g. "incidentbot@yourcompany.com")
    public string SenderMailbox { get; set; } = "";
    // Comma-separated fallback when an incident has no Recipients set
    public string DefaultRecipients { get; set; } = "";
}
