using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using PSS_Time_Tracker;
using PSS_Time_Tracker.Data;
using Hangfire;
using Hangfire.SqlServer;

var builder = WebApplication.CreateBuilder(args);

// --------------------------------------------------------------------------------------------
// AUTHENTICATION
// The original app used Azure AD (Microsoft.Identity.Web) + Microsoft Graph here. That's
// replaced with plain cookie authentication: the AccountController's Login action signs a
// user in directly from a seeded list of local test accounts (see SeedData.cs). Everything
// downstream (Controllers, [Authorize], the "RequireManagerRole" policy) keeps working the
// same way because it only ever depended on the resulting ClaimsPrincipal, not on Azure AD
// specifically.
// --------------------------------------------------------------------------------------------
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/SignOut";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireManagerRole", policy =>
        policy.RequireClaim("groups", builder.Configuration["ManagerGroupId"] ?? "local-managers"));
});

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<PSS_Time_Tracker.Data.timeSheetRecorderContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConn")));

builder.Services.AddHangfire(config =>
    config.UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConn")));
builder.Services.AddHangfireServer();

builder.Services.AddTransient<EmailService>();

var app = builder.Build();

// Apply any pending EF Core migrations and make sure a handful of test employees/managers
// and host companies exist, so the app is usable immediately after `dotnet run`.
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
