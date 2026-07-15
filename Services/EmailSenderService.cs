using Azure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Graph.Users.Item.SendMail;
using SFSWebForm.Models;

namespace SFSWebForm.Services;

public class EmailSenderService(IOptions<MailSettings> opts, ILogger<EmailSenderService> logger)
{
    private readonly MailSettings _settings = opts.Value;

    // callerIdentity: the signed-in user's email (or "user@domain" username) from their Entra ID claims
    public async Task SendAsync(IncidentEmail email, string callerIdentity, string? emailRecipients)
    {
        logger.LogInformation("Attempting to send email {EmailId} for identity '{Identity}'", email.Id, callerIdentity);

        if (string.IsNullOrWhiteSpace(_settings.TenantId) ||
            string.IsNullOrWhiteSpace(_settings.ClientId) ||
            string.IsNullOrWhiteSpace(_settings.ClientSecret))
            throw new InvalidOperationException(
                "Azure credentials not configured. Set TenantId, ClientId, and ClientSecret in appsettings.json.");

        var normalizedIdentity = string.IsNullOrWhiteSpace(callerIdentity) ? "" : callerIdentity.Trim();

        // Send as the signed-in user so notifications accurately reflect who actually dispatched
        // them — this was previously backwards: SenderMailbox took priority unconditionally, so
        // every send went out from that one configured mailbox regardless of who clicked Send.
        // SenderMailbox is now only a fallback for the rare case identity isn't a real email
        // (e.g. a non-interactive caller with no signed-in user context).
        var senderEmail = normalizedIdentity.Contains('@') ? normalizedIdentity : _settings.SenderMailbox;

        if (string.IsNullOrWhiteSpace(senderEmail))
            throw new InvalidOperationException(
                "Could not determine a sender mailbox: the signed-in user has no email claim, and no SenderMailbox fallback is configured in appsettings.json.");

        logger.LogInformation("Resolved sender email '{SenderEmail}' from identity '{Identity}'", senderEmail, normalizedIdentity);

        var recipientString = string.IsNullOrWhiteSpace(emailRecipients)
            ? _settings.DefaultRecipients
            : emailRecipients;

        if (string.IsNullOrWhiteSpace(recipientString))
            throw new InvalidOperationException(
                "No recipients configured. Set DefaultRecipients in appsettings.json or enter recipients on this email.");

        var toRecipients = recipientString
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(addr => new Recipient { EmailAddress = new EmailAddress { Address = addr } })
            .ToList();

        logger.LogInformation("Sending email {EmailId} via Graph: sender={SenderEmail} recipients=[{Recipients}] tenant={TenantId} clientId={ClientId}",
            email.Id, senderEmail, recipientString, _settings.TenantId, _settings.ClientId);

        var credential = new ClientSecretCredential(
            _settings.TenantId, _settings.ClientId, _settings.ClientSecret);
        var graphClient = new GraphServiceClient(credential);

        var subjectPrefix = email.Priority switch
        {
            EmailPriority.Critical => "[CRITICAL] ",
            EmailPriority.Important => "[IMPORTANT] ",
            _ => ""
        };
        var importance = email.Priority == EmailPriority.Normal ? Importance.Normal : Importance.High;

        try
        {
            await graphClient.Users[senderEmail].SendMail.PostAsync(new SendMailPostRequestBody
            {
                Message = new Message
                {
                    Subject = subjectPrefix + email.Subject,
                    Body = new ItemBody { ContentType = BodyType.Html, Content = BuildHtmlBody(email) },
                    ToRecipients = toRecipients,
                    From = new Recipient { EmailAddress = new EmailAddress { Address = senderEmail } },
                    Importance = importance
                },
                SaveToSentItems = true
            });
        }
        catch (ODataError odataEx)
        {
            // Graph's own error payload (code/message) is far more actionable than the generic
            // HTTP exception message — e.g. "ErrorAccessDenied", "MailboxNotEnabledForRESTAPI",
            // or "Authorization_RequestDenied" when admin consent for Mail.Send is missing.
            logger.LogError(odataEx,
                "Graph API rejected send for email {EmailId} as {SenderEmail}. Status={StatusCode} Code={ErrorCode} Message={ErrorMessage}",
                email.Id, senderEmail, odataEx.ResponseStatusCode, odataEx.Error?.Code, odataEx.Error?.Message);
            throw;
        }

        logger.LogInformation("Email {EmailId} sent as {Sender} to {Recipients}",
            email.Id, senderEmail, recipientString);
    }

    // The stored Body stays plain text (simple to edit in a textarea) — this wraps it in a
    // styled HTML shell only at send time. Only the colored banner is boxed; the message body
    // itself flows like a normal email (no card/border around it, no monospace font) rather than
    // looking like "an email inside an email." Section headers (e.g. "ROOT CAUSE:") and field
    // labels (e.g. "Time of Outage:") get bolded/underlined automatically based on the composer's
    // known formatting conventions.
    private static string BuildHtmlBody(IncidentEmail email)
    {
        var (headerColor, eyebrow) = email.Type switch
        {
            EmailType.InitialOutage => ("#c0392b", "Service Disruption"),
            EmailType.Update => ("#b35c00", "Service Disruption Update"),
            EmailType.Resolution => ("#1e7e34", "Service Restored"),
            _ => ("#495057", "Notification")
        };

        var priorityBanner = email.Priority switch
        {
            EmailPriority.Critical =>
                "<div style=\"background:#f8d7da;color:#842029;padding:10px 28px;font-weight:700;font-size:13px;letter-spacing:.03em;\">&#128680; CRITICAL &mdash; immediate attention required</div>",
            EmailPriority.Important =>
                "<div style=\"background:#fff3cd;color:#664d03;padding:10px 28px;font-weight:600;font-size:13px;letter-spacing:.03em;\">&#9888; IMPORTANT</div>",
            _ => ""
        };

        var application = System.Net.WebUtility.HtmlEncode(email.Incident.Application);
        var encodedEyebrow = System.Net.WebUtility.HtmlEncode(eyebrow);
        var bodyHtml = string.Join("<br>", email.Body
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(FormatBodyLine));

        return $"""
            <div style="font-family:'Segoe UI',Arial,sans-serif;max-width:680px;">
              <div style="background:{headerColor};color:#ffffff;padding:16px 24px;">
                <div style="font-size:12px;font-weight:600;letter-spacing:.06em;text-transform:uppercase;opacity:.85;">{encodedEyebrow}</div>
                <div style="font-size:20px;font-weight:700;margin-top:2px;">{application}</div>
              </div>
              {priorityBanner}
              <div style="padding:20px 4px;color:#212529;font-size:14px;line-height:1.6;">{bodyHtml}</div>
            </div>
            """;
    }

    private static readonly System.Text.RegularExpressions.Regex KeyValueLine =
        new(@"^(\s*)([^:]{1,40}:)(\s{2,})(.+)$");

    private static string FormatBodyLine(string rawLine)
    {
        var trimmed = rawLine.Trim();

        // Standalone all-caps section headers the composer emits on their own line,
        // e.g. "NEXT STEPS:", "ROOT CAUSE:", "UPDATE:"
        if (trimmed.Length > 1 && trimmed.EndsWith(':') && trimmed.Any(char.IsLetter) && trimmed == trimmed.ToUpperInvariant())
            return $"<strong><u>{EncodeAndPreserveSpaces(trimmed)}</u></strong>";

        // Aligned "  Label:     Value" lines the composer emits for key facts — bold just the label
        var match = KeyValueLine.Match(rawLine);
        if (match.Success)
        {
            return EncodeAndPreserveSpaces(match.Groups[1].Value)
                + "<strong>" + EncodeAndPreserveSpaces(match.Groups[2].Value) + "</strong>"
                + EncodeAndPreserveSpaces(match.Groups[3].Value)
                + EncodeAndPreserveSpaces(match.Groups[4].Value);
        }

        return EncodeAndPreserveSpaces(rawLine);
    }

    // HTML-encodes, then converts runs of 2+ spaces to &nbsp; so manually-aligned spacing survives
    // in email clients that don't honor white-space CSS — single spaces stay normal/wrappable.
    private static string EncodeAndPreserveSpaces(string text) =>
        System.Text.RegularExpressions.Regex.Replace(
            System.Net.WebUtility.HtmlEncode(text), "  +",
            m => string.Concat(Enumerable.Repeat("&nbsp;", m.Length)));
}
