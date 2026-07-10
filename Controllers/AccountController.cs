using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;

namespace SFSWebForm.Controllers;

public class AccountController(ILogger<AccountController> logger) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null, string? authError = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect(!string.IsNullOrWhiteSpace(returnUrl) ? returnUrl : Url.Action("Index", "Incidents")!);

        ViewData["ReturnUrl"] = returnUrl;
        ViewData["AuthError"] = authError == "1";
        return View();
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
