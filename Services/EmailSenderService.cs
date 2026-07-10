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

        try
        {
            await graphClient.Users[senderEmail].SendMail.PostAsync(new SendMailPostRequestBody
            {
                Message = new Message
                {
                    Subject = email.Subject,
                    Body = new ItemBody { ContentType = BodyType.Text, Content = email.Body },
                    ToRecipients = toRecipients,
                    From = new Recipient { EmailAddress = new EmailAddress { Address = senderEmail } }
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
}
