using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;

namespace SFSWebForm.Controllers;

public class AccountController(ILogger<AccountController> logger, IWebHostEnvironment env) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null, string? authError = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect(!string.IsNullOrWhiteSpace(returnUrl) ? returnUrl : Url.Action("Index", "Incidents")!);

        ViewData["ReturnUrl"] = returnUrl;
        ViewData["AuthError"] = authError == "1";
        ViewData["ShowDevSignIn"] = env.IsDevelopment();
        return View();
    }

    // Dev-only stand-in for the Microsoft sign-in challenge so the rest of the app (recipients,
    // send, logging) can be exercised before the Entra ID app registration's redirect URI is set up.
    // Gated on IsDevelopment() so it can never be reached once ASPNETCORE_ENVIRONMENT=Production
    // (as web.config already sets for the real IIS deployment).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DevSignIn(string? returnUrl = null)
    {
        if (!env.IsDevelopment())
            return NotFound();

        logger.LogWarning("DEV-ONLY sign-in used — bypassing Entra ID for local testing");

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "Test User"),
            new("DisplayName", "Test User"),
            new("UserEmail", "test.user@stellantis-fs.com"),
            new(ClaimTypes.Email, "test.user@stellantis-fs.com")
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return Redirect(!string.IsNullOrWhiteSpace(returnUrl) ? returnUrl : Url.Action("Index", "Incidents")!);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SignIn(string? returnUrl = null)
    {
        logger.LogInformation("Redirecting to Microsoft Entra ID sign-in (returnUrl: {ReturnUrl})", returnUrl);

        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(SignInCallback), new { returnUrl })
        };
        return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpGet]
    public IActionResult SignInCallback(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            logger.LogWarning("Sign-in callback reached without an authenticated principal");
            return RedirectToAction(nameof(Login));
        }

        logger.LogInformation("Login succeeded for '{DisplayName}' ({Email})",
            User.FindFirst("DisplayName")?.Value, User.FindFirst("UserEmail")?.Value);

        return Redirect(!string.IsNullOrWhiteSpace(returnUrl) ? returnUrl : Url.Action("Index", "Incidents")!);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        logger.LogInformation("Logout requested for '{DisplayName}'", User.FindFirst("DisplayName")?.Value);

        var properties = new AuthenticationProperties { RedirectUri = Url.Action(nameof(Login))! };
        return SignOut(properties, CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme);
    }
}
