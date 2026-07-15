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

        if (string.IsNullOrWhiteSpace(_settings.EmailDomain) && string.IsNullOrWhiteSpace(_settings.SenderMailbox))
            throw new InvalidOperationException("Either EmailDomain or SenderMailbox must be configured in appsettings.json.");

        var normalizedIdentity = string.IsNullOrWhiteSpace(callerIdentity) ? "" : callerIdentity.Trim();
        var senderEmail = _settings.SenderMailbox;

        if (string.IsNullOrWhiteSpace(senderEmail))
        {
            var username = normalizedIdentity.Contains('\\')
                ? normalizedIdentity.Split('\\').Last()
                : normalizedIdentity;

            if (normalizedIdentity.Contains('@'))
            {
                username = normalizedIdentity.Split('@')[0];
            }

            senderEmail = $"{username}@{_settings.EmailDomain}";
        }

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
    // styled HTML shell only at send time, so recipients get a nicer-looking email without the
    // edit UI ever needing to deal with raw HTML. Monospace + pre-wrap preserves the composer's
    // manually-aligned "  Label:   Value" lines, matching how the app's own preview renders it.
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
                "<div style=\"background:#f8d7da;color:#842029;padding:10px 28px;font-weight:700;font-size:13px;letter-spacing:.03em;border-bottom:1px solid #f5c2c7;\">&#128680; CRITICAL &mdash; immediate attention required</div>",
            EmailPriority.Important =>
                "<div style=\"background:#fff3cd;color:#664d03;padding:10px 28px;font-weight:600;font-size:13px;letter-spacing:.03em;border-bottom:1px solid #ffe69c;\">&#9888; IMPORTANT</div>",
            _ => ""
        };

        var application = System.Net.WebUtility.HtmlEncode(email.Incident.Application);
        var encodedEyebrow = System.Net.WebUtility.HtmlEncode(eyebrow);
        var encodedBody = System.Net.WebUtility.HtmlEncode(email.Body);

        return $"""
            <div style="font-family:'Segoe UI',Arial,sans-serif;max-width:680px;margin:0 auto;border:1px solid #dee2e6;border-radius:8px;overflow:hidden;">
              <div style="background:{headerColor};color:#ffffff;padding:20px 28px;">
                <div style="font-size:12px;font-weight:600;letter-spacing:.06em;text-transform:uppercase;opacity:.85;">{encodedEyebrow}</div>
                <div style="font-size:22px;font-weight:700;margin-top:4px;">{application}</div>
              </div>
              {priorityBanner}
              <div style="padding:24px 28px;color:#212529;font-size:14px;line-height:1.6;white-space:pre-wrap;font-family:Consolas,'Courier New',monospace;">{encodedBody}</div>
            </div>
            """;
    }
}
