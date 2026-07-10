using Azure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;
using SFSWebForm.Models;

namespace SFSWebForm.Services;

public class EmailSenderService(IOptions<MailSettings> opts, ILogger<EmailSenderService> logger)
{
    private readonly MailSettings _settings = opts.Value;

    // windowsIdentity: HttpContext.User.Identity.Name — e.g. "CORP\jsmith"
    public async Task SendAsync(IncidentEmail email, string windowsIdentity, string? incidentRecipients)
    {
        logger.LogInformation("Attempting to send email {EmailId} for identity '{Identity}'", email.Id, windowsIdentity);

        if (string.IsNullOrWhiteSpace(_settings.TenantId) ||
            string.IsNullOrWhiteSpace(_settings.ClientId) ||
            string.IsNullOrWhiteSpace(_settings.ClientSecret))
            throw new InvalidOperationException(
                "Azure credentials not configured. Set TenantId, ClientId, and ClientSecret in appsettings.json.");

        if (string.IsNullOrWhiteSpace(_settings.EmailDomain) && string.IsNullOrWhiteSpace(_settings.SenderMailbox))
            throw new InvalidOperationException("Either EmailDomain or SenderMailbox must be configured in appsettings.json.");

        var normalizedIdentity = string.IsNullOrWhiteSpace(windowsIdentity) ? "" : windowsIdentity.Trim();
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

        var recipientString = string.IsNullOrWhiteSpace(incidentRecipients)
            ? _settings.DefaultRecipients
            : incidentRecipients;

        if (string.IsNullOrWhiteSpace(recipientString))
            throw new InvalidOperationException(
                "No recipients configured. Set DefaultRecipients in appsettings.json or enter recipients on the incident.");

        var toRecipients = recipientString
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(addr => new Recipient { EmailAddress = new EmailAddress { Address = addr } })
            .ToList();

        logger.LogInformation("Sending email {EmailId} from {SenderEmail} to {Recipients}", email.Id, senderEmail, recipientString);

        var credential = new ClientSecretCredential(
            _settings.TenantId, _settings.ClientId, _settings.ClientSecret);
        var graphClient = new GraphServiceClient(credential);

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

        logger.LogInformation("Email {EmailId} sent as {Sender} to {Recipients}",
            email.Id, senderEmail, recipientString);
    }
}
