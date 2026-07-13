using Azure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models.ODataErrors;
using SFSWebForm.Models;

namespace SFSWebForm.Services;

public class DirectoryUser
{
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
}

// Looks up people in the org directory (via Microsoft Graph) so recipients can be picked by name
// instead of typed/hardcoded. Requires the "User.Read.All" Application permission (admin-consented)
// on the same app registration used for Mail.Send in MailSettings.
public class DirectoryService(IOptions<MailSettings> opts, ILogger<DirectoryService> logger)
{
    private readonly MailSettings _settings = opts.Value;

    public async Task<List<DirectoryUser>> SearchUsersAsync(string? query, int top = 8)
    {
        var term = (query ?? "").Trim().Replace("\"", "");
        if (term.Length < 2)
            return new List<DirectoryUser>();

        if (string.IsNullOrWhiteSpace(_settings.TenantId) ||
            string.IsNullOrWhiteSpace(_settings.ClientId) ||
            string.IsNullOrWhiteSpace(_settings.ClientSecret))
        {
            logger.LogWarning("Directory search skipped for '{Query}' — Azure credentials not configured", term);
            return new List<DirectoryUser>();
        }

        var credential = new ClientSecretCredential(_settings.TenantId, _settings.ClientId, _settings.ClientSecret);
        var graphClient = new GraphServiceClient(credential);

        try
        {
            var result = await graphClient.Users.GetAsync(config =>
            {
                config.QueryParameters.Search = $"\"displayName:{term}\" OR \"mail:{term}\"";
                config.QueryParameters.Filter = "accountEnabled eq true";
                config.QueryParameters.Select = ["displayName", "mail", "userPrincipalName"];
                config.QueryParameters.Top = top;
                config.Headers.Add("ConsistencyLevel", "eventual");
            });

            return result?.Value?
                .Select(u => new DirectoryUser { DisplayName = u.DisplayName ?? "", Email = u.Mail ?? u.UserPrincipalName ?? "" })
                .Where(u => !string.IsNullOrWhiteSpace(u.Email))
                .ToList() ?? [];
        }
        catch (ODataError ex)
        {
            logger.LogError(ex, "Directory search failed for '{Query}'. Code={ErrorCode} Message={ErrorMessage}",
                term, ex.Error?.Code, ex.Error?.Message);
            return [];
        }
    }
}
