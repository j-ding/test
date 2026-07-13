using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using SFSWebForm.Data;
using SFSWebForm.Models;
using SFSWebForm.Services;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/sfs-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("SFS Incident Manager starting up");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            path: "logs/sfs-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm} [{Level:u3}] {Message:lj}{NewLine}{Exception}"));

    var azureAd = builder.Configuration.GetSection("AzureAd");
    var testingSettings = builder.Configuration.GetSection("Testing").Get<TestingSettings>() ?? new TestingSettings();

    builder.Services.AddAuthentication(options =>
        {
            // Cookie stays the default challenge scheme so an unauthenticated [Authorize] hit
            // redirects to our /Account/Login portal page first, not straight to Microsoft.
            // The portal's "Sign in with Microsoft" button explicitly challenges OpenIdConnect.
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultSignOutScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.AccessDeniedPath = "/Account/Login";
            options.ReturnUrlParameter = "returnUrl";
        })
        .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            var tenantId = azureAd["TenantId"];
            options.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
            options.ClientId = azureAd["ClientId"];
            options.ClientSecret = azureAd["ClientSecret"];
            options.ResponseType = "code";
            options.CallbackPath = azureAd["CallbackPath"] ?? "/signin-oidc";
            options.SignedOutCallbackPath = "/signout-callback-oidc";
            options.SaveTokens = true;
            options.UsePkce = true;
            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");
            options.TokenValidationParameters = new TokenValidationParameters
            {
                NameClaimType = "name"
            };
            options.Events = new OpenIdConnectEvents
            {
                OnTokenValidated = ctx =>
                {
                    var principal = ctx.Principal;
                    if (principal == null) return Task.CompletedTask;

                    var identity = (System.Security.Claims.ClaimsIdentity)principal.Identity!;
                    var displayName = principal.FindFirst("name")?.Value
                        ?? principal.Identity?.Name
                        ?? string.Empty;
                    var email = principal.FindFirst("preferred_username")?.Value
                        ?? principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                        ?? string.Empty;

                    identity.AddClaim(new System.Security.Claims.Claim("DisplayName", displayName));
                    identity.AddClaim(new System.Security.Claims.Claim("UserEmail", email));
                    if (!principal.HasClaim(c => c.Type == System.Security.Claims.ClaimTypes.Email) && !string.IsNullOrWhiteSpace(email))
                        identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, email));

                    return Task.CompletedTask;
                },
                OnRemoteFailure = ctx =>
                {
                    ctx.Response.Redirect("/Account/Login?authError=1");
                    ctx.HandleResponse();
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization();

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=sfs_incidents.db"));

    builder.Services.Configure<MailSettings>(builder.Configuration.GetSection("MailSettings"));
    builder.Services.Configure<TestingSettings>(builder.Configuration.GetSection("Testing"));
    builder.Services.AddScoped<EmailComposerService>();
    builder.Services.AddScoped<EmailSenderService>();
    builder.Services.AddControllersWithViews();

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
    }

    if (!app.Environment.IsDevelopment())
        app.UseHsts();

    if (testingSettings.DebugMode)
        app.UseDeveloperExceptionPage();
    else if (!app.Environment.IsDevelopment())
        app.UseExceptionHandler("/Home/Error");

    app.UseSerilogRequestLogging(opts =>
    {
        opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0}ms";
    });

    app.UseHttpsRedirection();
    app.UseRouting();
    app.UseAuthentication();

    if (!testingSettings.WindowsAuthenticationEnabled)
    {
        Log.Warning("Testing:WindowsAuthenticationEnabled is false — sign-in is bypassed for ALL requests. Do not leave this off on a real deployment.");

        // Overrides whatever the real auth handlers determined (normally "anonymous", since no
        // one can complete the Entra ID challenge here) with a fixed stand-in identity, so the
        // site can be exercised without going through sign-in at all.
        app.Use(async (context, next) =>
        {
            var claims = new List<System.Security.Claims.Claim>
            {
                new(System.Security.Claims.ClaimTypes.Name, "Test User"),
                new("DisplayName", "Test User"),
                new("UserEmail", "test.user@stellantis-fs.com"),
                new(System.Security.Claims.ClaimTypes.Email, "test.user@stellantis-fs.com")
            };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "TestingBypass");
            context.User = new System.Security.Claims.ClaimsPrincipal(identity);
            await next();
        });
    }

    app.UseAuthorization();
    app.MapStaticAssets();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Incidents}/{action=Index}/{id?}")
        .WithStaticAssets();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "SFS Incident Manager terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
