namespace SFSWebForm.Models;

// Config-driven testing toggles, independent of ASPNETCORE_ENVIRONMENT — usable on any deployed
// instance (e.g. the shared test/stage IIS server), not just local Development.
public class TestingSettings
{
    // Shows full exception details (Developer Exception Page) instead of the generic error page.
    public bool DebugMode { get; set; } = false;

    // When false, sign-in is bypassed entirely and every request is treated as an authenticated
    // stand-in "Test User" — lets the site be exercised without going through Entra ID at all.
    // Leave true for any real deployment.
    public bool WindowsAuthenticationEnabled { get; set; } = true;
}
