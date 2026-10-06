using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using PSS_Time_Tracker;
using PSS_Time_Tracker.Data;
using PSS_Time_Tracker.Services;
using Hangfire;
using Hangfire.SqlServer;

var builder = WebApplication.CreateBuilder(args);

// Data Protection keys encrypt every auth-related cookie this app issues - the final sign-in
// cookie, and (for the Azure AD flow below) the short-lived nonce/correlation cookies used
// mid-handshake. Left at the default (in-memory, regenerated every process start), restarting
// the app - a rebuild, a debugger relaunch - between being redirected to Microsoft and coming
// back invalidates whatever cookie was issued before the restart, surfacing as a flatly unhelpful
// "Correlation failed." error that has nothing to do with anything actually misconfigured.
// Persisting keys to a stable folder outside bin/obj (so a rebuild/clean never wipes them) means
// a restart mid-test no longer breaks an in-flight sign-in.
builder.Services.AddDataProtection()
    .SetApplicationName("PSS_Time_Tracker")
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PSS_Time_Tracker", "DataProtection-Keys")));

// --------------------------------------------------------------------------------------------
// AUTHENTICATION
// Sign-in is Azure AD (Microsoft.Identity.Web) against the providencesoft.com tenant, whenever
// AzureAd:ClientId/TenantId are configured (appsettings.json - the client secret itself lives
// only in User Secrets, never committed to the repo). OnTokenValidated below looks the signed-in
// person up in the local Users table by email and builds the app's own claims from it via
// AppIdentityFactory, so every [Authorize]/RequireManagerRole/RequireHrRole check downstream, and
// User.GetUserId(), work off one consistent shape. Azure AD only ever proves who someone is here
// (passwords and MFA stay entirely in Entra ID) - never what they're allowed to do, and never
// whether they're allowed in at all: a real person Azure AD authenticates who isn't already a row
// in Users is refused outright. SQL holds nothing but a profile row per employee - no credentials,
// no identity data of its own.
//
// Falls back to plain cookie authentication with no sign-in page of its own wired up (see
// AccountController) only when AzureAd isn't configured at all, e.g. a fresh clone of this repo
// with no App Registration set up yet - not a usable alternative login path, just what keeps the
// app from crashing at startup before that's done.
// --------------------------------------------------------------------------------------------
var azureAdConfigured =
    !string.IsNullOrWhiteSpace(builder.Configuration["AzureAd:ClientId"]) &&
    !string.IsNullOrWhiteSpace(builder.Configuration["AzureAd:TenantId"]);

if (azureAdConfigured)
{
    // Microsoft.Identity.Web 4.x only exposes AddMicrosoftIdentityWebApp as an AuthenticationBuilder
    // extension, so it has to be chained after AddAuthentication(...) here - but that initial call's
    // own scheme argument still wins as DefaultAuthenticateScheme/DefaultSignInScheme, and
    // OpenIdConnect is a challenge-only/remote handler that returns NoResult() for every actual
    // request. Left alone, that means even a perfectly valid mock-login cookie is never checked at
    // all - every request bounces straight to Microsoft's login page regardless of whether the
    // cookie was there - which is exactly what happened until the explicit Configure<AuthenticationOptions>
    // below re-asserts Cookies as the scheme that answers "is this request already signed in".
    builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

    // Auto-provisions a not-yet-seen sign-in straight from the tenant directory - see
    // OnTokenValidated below and AzureAdProvisioningService's own doc comment.
    builder.Services.AddHttpClient<IAzureAdProvisioningService, AzureAdProvisioningService>();

    builder.Services.Configure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(options =>
    {
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    });

    builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/SignOut";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

    builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        // Look the authenticated person up locally and swap in our own claim shape - see the
        // block comment above and AppIdentityFactory for why.
        options.Events.OnTokenValidated = async context =>
        {
            var email = context.Principal?.FindFirst(ClaimTypes.Upn)?.Value
                ?? context.Principal?.FindFirst("preferred_username")?.Value
                ?? context.Principal?.FindFirst(ClaimTypes.Email)?.Value;

            var db = context.HttpContext.RequestServices.GetRequiredService<timeSheetRecorderContext>();
            var user = !string.IsNullOrWhiteSpace(email)
                ? await db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower())
                : null;

            if (user == null)
            {
                // Not provisioned locally yet - check the tenant directory itself before refusing.
                // Azure AD already knows every real employee, so a real person it just authenticated
                // shouldn't need a human to separately create their row first (see
                // AzureAdProvisioningService's own doc comment). Only ever provisions a plain employee -
                // elevating someone to manager/HR is still a deliberate step afterward, not something a
                // directory lookup grants.
                var provisioning = context.HttpContext.RequestServices.GetRequiredService<IAzureAdProvisioningService>();
                var newAccount = !string.IsNullOrWhiteSpace(email) ? await provisioning.TryBuildProfileAsync(email) : null;

                if (newAccount == null)
                {
                    context.Fail($"'{email ?? "(no email claim)"}' isn't set up in HourTrack yet and couldn't be " +
                        "found in the company directory either. Contact HR/IT.");
                    return;
                }

                db.Users.Add(newAccount);
                await db.SaveChangesAsync();
                user = newAccount;
            }

            // If Azure AD actually included a "groups" claim in the token (Entra ID -> App
            // registrations -> Token configuration -> Add groups claim - free, no extra Graph
            // permission needed, unlike reading group membership via a live Graph call), and it
            // names the configured ManagerGroupId, that's this employee's real, current group
            // membership overriding whatever IsManager was last set to - manager access should
            // track the group, not lag behind it until someone remembers to flip the local flag.
            // Only ever upgrades, never revokes: an absent claim just means the group claim isn't
            // configured yet (or Azure's "groups overage" omitted it because this user is in too
            // many groups to list), not evidence they were removed from it, so IsManager=false set
            // by an admin still holds when there's no groups claim to check at all.
            var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var managerGroupId = configuration["ManagerGroupId"];
            var isInManagerGroup = !string.IsNullOrWhiteSpace(managerGroupId) &&
                (context.Principal?.FindAll("groups").Any(c => c.Value == managerGroupId) ?? false);
            if (isInManagerGroup && !user.IsManager)
            {
                user.IsManager = true;
                await db.SaveChangesAsync();
            }

            context.Principal = new ClaimsPrincipal(AppIdentityFactory.BuildIdentity(user, configuration, "AzureAD"));
        };

        // A failed OnTokenValidated (the context.Fail above) would otherwise surface as an
        // unhandled 500 - send it back to the login screen with a plain-language reason instead.
        options.Events.OnRemoteFailure = context =>
        {
            context.Response.Redirect("/Account/Login?authError=" + Uri.EscapeDataString(context.Failure?.Message ?? "Sign-in failed. Please try again."));
            context.HandleResponse();
            return Task.CompletedTask;
        };
    });
}
else
{
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/SignOut";
            options.AccessDeniedPath = "/Account/Login";
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
        });
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireManagerRole", policy =>
        policy.RequireClaim("groups", builder.Configuration["ManagerGroupId"] ?? "local-managers"));

    // Separate from RequireManagerRole - see UserAccount.IsHr. Gates the HR Board (HR decision +
    // Payroll capture stages of leave approval), distinct from a line manager's own "Approve Time Off".
    options.AddPolicy("RequireHrRole", policy =>
        policy.RequireClaim("groups", builder.Configuration["HrGroupId"] ?? "local-hr"));
});

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<PSS_Time_Tracker.Data.timeSheetRecorderContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConn")));

builder.Services.AddHangfire(config =>
    config.UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConn"))); 
builder.Services.AddHangfireServer();

builder.Services.AddHttpClient<EmailService>();
builder.Services.AddHttpClient<ITeamsNotificationService, TeamsNotificationService>(c => c.Timeout = TimeSpan.FromSeconds(8));
builder.Services.AddTransient<ITimesheetPdfService, TimesheetPdfService>();
builder.Services.AddTransient<ILeaveRequestPdfService, LeaveRequestPdfService>();
builder.Services.AddScoped<IPublicHolidayService, PublicHolidayService>();
builder.Services.AddScoped<IWeeklyReportGapAnalysisService, WeeklyReportGapAnalysisService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ICheckOutSyncService, CheckOutSyncService>();

// SharePointIntegration:UseMockData (see appsettings.Development.json) lets the whole SharePoint/Azure
// AD flow - Capture Your Timesheet's check-in pull, Request Time Off's real balances, Job Title/
// Department - be tested with no live Graph credentials at all. Every value it returns is prefixed
// "(Mock)" so it's obvious this is on. Flip it off (or just don't set it) once real credentials are
// configured, so production never accidentally serves mock data. See docs/SharePointIntegration.md.
if (builder.Configuration.GetValue<bool>("SharePointIntegration:UseMockData"))
{
    builder.Services.AddSingleton<ISharePointCheckInService, MockSharePointCheckInService>();
    builder.Services.AddSingleton<ISharePointLeaveBalanceService, MockSharePointLeaveBalanceService>();
    builder.Services.AddSingleton<IAzureAdProfileService, MockAzureAdProfileService>();
}
else
{
    builder.Services.AddHttpClient<ISharePointCheckInService, SharePointCheckInService>();
    builder.Services.AddHttpClient<ISharePointLeaveBalanceService, SharePointLeaveBalanceService>();
    builder.Services.AddHttpClient<IAzureAdProfileService, AzureAdProfileService>();
}

var app = builder.Build();

// Picks up check-outs for timesheets that were started before the employee checked out.
app.Services.GetRequiredService<Hangfire.IRecurringJobManager>().AddOrUpdate<ICheckOutSyncService>(
    "sync-pending-checkouts", s => s.SyncAllPendingAsync(), "*/15 * * * *");

// Surfaces a half-finished notification setup at startup instead of only as per-request failures.
{
    var startupLog = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    var usingSmtp = !string.IsNullOrWhiteSpace(app.Configuration["EmailSettings:SmtpServer"]);
    var emailReady = !string.IsNullOrWhiteSpace(app.Configuration["EmailSettings:Username"] ?? app.Configuration["EmailSettings:SenderAddress"]) &&
        (usingSmtp ? !string.IsNullOrWhiteSpace(app.Configuration["EmailSettings:Password"])
                   : !string.IsNullOrWhiteSpace(app.Configuration["AzureAd:ClientSecret"]));
    if (!emailReady)
    {
        startupLog.LogWarning("Email notifications are OFF: set EmailSettings:Username and EmailSettings:Password " +
            "(SMTP), or blank SmtpServer and use the Graph Mail.Send route.");
    }
    if (string.IsNullOrWhiteSpace(app.Configuration["Teams:FlowUrl"]))
    {
        startupLog.LogWarning("Teams notifications are OFF: set Teams:FlowUrl to the Power Automate flow's HTTP URL.");
    }
}

// Apply any pending EF Core migrations and make sure a handful of test employees/managers,
// leave types, and leave balances exist, so the app is usable immediately after `dotnet run`.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<timeSheetRecorderContext>();
    db.Database.Migrate();
    SeedData.EnsureSeeded(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

try
{
    app.Run();
}
catch (Exception ex)
{
    Console.Error.WriteLine("Unhandled exception running app: " + ex);
    throw;
}
