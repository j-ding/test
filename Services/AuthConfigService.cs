using Microsoft.Extensions.Configuration;

namespace SFSWebForm.Services;

public class AuthConfigService(IConfiguration configuration)
{
    public IReadOnlyList<AuthUser> Users { get; } = LoadUsers(configuration);

    public AuthUser? FindUser(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return null;

        var normalized = identifier.Trim();
        return Users.FirstOrDefault(user =>
            string.Equals(user.Username, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(user.Email, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(user.DisplayName, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(user.Email, $"{normalized}@stellantis-fs.com", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<AuthUser> LoadUsers(IConfiguration configuration)
    {
        var users = new List<AuthUser>();

        var allowListSection = configuration.GetSection("Authentication:AllowList");
        foreach (var child in allowListSection.GetChildren())
        {
            var username = child["Username"] ?? child.Key;
            var user = new AuthUser
            {
                Username = username,
                DisplayName = child["DisplayName"] ?? username,
                Email = child["Email"] ?? string.Empty
            };

            users.Add(user);
        }

        return users;
    }
}

public class AuthUser
{
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
