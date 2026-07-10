using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SFSWebForm.Models;
using SFSWebForm.Services;

namespace SFSWebForm.Controllers;

public class AccountController(ILogger<AccountController> logger, AuthConfigService authConfig) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var username = model.Username.Trim();
        if (string.IsNullOrWhiteSpace(username) && User.Identity?.IsAuthenticated == true)
        {
            username = User.Identity.Name ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            ModelState.AddModelError(string.Empty, "Username or email is required.");
            return View(model);
        }

        if (!TryAuthenticateAllowedUser(username, out var displayName, out var email, out var failureReason))
        {
            logger.LogWarning("Login failed for user '{Username}'. Reason: {Reason}", username, failureReason);
            ModelState.AddModelError(string.Empty, "Your account is not authorized for this app.");
            return View(model);
        }

        logger.LogInformation("Login succeeded for user '{Username}' as '{DisplayName}' ({Email})", username, displayName, email);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, displayName),
            new(ClaimTypes.NameIdentifier, username),
            new(ClaimTypes.Email, email),
            new("DisplayName", displayName),
            new("UserEmail", email)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            RedirectUri = model.ReturnUrl ?? Url.Action("Index", "Incidents")
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

        return Redirect(!string.IsNullOrWhiteSpace(model.ReturnUrl) ? model.ReturnUrl : Url.Action("Index", "Incidents")!);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login", "Account");
    }

    private bool TryAuthenticateAllowedUser(string username, out string displayName, out string email, out string failureReason)
    {
        displayName = string.Empty;
        email = string.Empty;
        failureReason = "Unknown";

        if (string.IsNullOrWhiteSpace(username))
        {
            failureReason = "Missing username or email";
            return false;
        }

        var trimmed = username.Trim();
        logger.LogInformation("Checking allow-list login for '{Username}'", trimmed);

        var configuredUser = authConfig.FindUser(trimmed);
        if (configuredUser == null)
        {
            failureReason = "User is not configured for this app";
            return false;
        }

        displayName = configuredUser.DisplayName;
        email = configuredUser.Email;
        failureReason = string.Empty;
        return true;
    }
}
