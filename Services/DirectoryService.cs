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
//
// Registered as a singleton (see Program.cs) so the GraphServiceClient/credential — and the token
// cache Azure.Identity keeps inside it — survive across requests. Building a fresh
// ClientSecretCredential per search forces a brand-new OAuth token round-trip to Azure AD on every
// keystroke-driven search, which is what made results feel slow.
public class DirectoryService(IOptions<MailSettings> opts, ILogger<DirectoryService> logger)
{
    private readonly MailSettings _settings = opts.Value;
    private readonly Lazy<GraphServiceClient?> _graphClient = new(() =>
    {
        var s = opts.Value;
        if (string.IsNullOrWhiteSpace(s.TenantId) ||
            string.IsNullOrWhiteSpace(s.ClientId) ||
            string.IsNullOrWhiteSpace(s.ClientSecret))
            return null;

        var credential = new ClientSecretCredential(s.TenantId, s.ClientId, s.ClientSecret);
        return new GraphServiceClient(credential);
    });

    public async Task<List<DirectoryUser>> SearchUsersAsync(string? query, int top = 8)
    {
        var term = (query ?? "").Trim().Replace("\"", "");
        if (term.Length < 2)
            return new List<DirectoryUser>();

        var graphClient = _graphClient.Value;
        if (graphClient == null)
        {
            logger.LogWarning("Directory search skipped for '{Query}' — Azure credentials not configured", term);
            return new List<DirectoryUser>();
        }

        try
        {
            // No accountEnabled filter: shared mailboxes (e.g. applicationproductionsupport@...)
            // get an Azure AD user object like anyone else, but Microsoft disables direct sign-in
            // on them by default, so filtering to accountEnabled eq true was hiding them.
            var result = await graphClient.Users.GetAsync(config =>
            {
                config.QueryParameters.Search = $"\"displayName:{term}\" OR \"mail:{term}\"";
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
