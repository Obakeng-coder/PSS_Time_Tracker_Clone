# PSS TymSheet — local learning copy (no Azure AD)

This is the original PSS Internal Time Tracker app with **one thing changed**: Azure AD /
Microsoft Graph sign-in has been replaced with a simple local "Login as" screen backed by a
few seeded test accounts. Everything else — the controllers, stored procedures, Hangfire
background job, PDF export, email notifications, EF Core migrations, views — is the same
code, so you can trace the real flow end to end.

## What actually changed vs. the original

| Area | Original | This copy |
|---|---|---|
| Sign-in | Redirect to Azure AD (`Microsoft.Identity.Web`) | `AccountController.Login` — pick a seeded test account from a dropdown |
| Employee profile data (name, job title, manager) | Fetched live from Microsoft Graph on `Create` | Read from the local `Users` table (populated by `SeedData.cs`) |
| "Is this person a manager?" | Azure AD group membership (`groups` claim) | `UserAccount.IsManager` column, still surfaced as the same `groups` claim so the rest of the app (the `RequireManagerRole` policy, the sidebar) doesn't know the difference |
| Everything else (stored procs, Hangfire retry job, iTextSharp PDF report, SMTP emails) | unchanged | unchanged |

Nothing about the *shape* of the flow changed — an employee still submits a timesheet, the
row lands in `TimeTracker`, and a manager's board still queries it back out via
`GetManagerEmployeeTimeSheetsPaged`. Swapping the auth provider didn't need to touch that.

**Also worth knowing:** the original repo's `appsettings.json` had a live-looking Azure AD
client secret checked into source control. That's stripped out here — don't reuse it, and if
that tenant is real, whoever owns it should rotate the secret.

## Prerequisites

- .NET 8 SDK
- SQL Server (Express, Developer edition, or LocalDB all work) running locally
- (Optional) SQL Server Management Studio / Azure Data Studio, to run the stored-procedure script

## Setup

1. **Point the connection string at your SQL Server.**
   Edit `PSS_Time_Tracker/appsettings.json` → `ConnectionStrings:DefaultConn` if
   `Server=localhost\SQLEXPRESS` doesn't match your setup.

2. **Create the schema.**
   ```bash
   cd PSS_Time_Tracker
   dotnet tool install --global dotnet-ef   # if you don't already have it
   dotnet ef database update
   ```
   This creates the `Timesheet` database and all tables (`Users`, `TimeTracker`,
   `HostCompanies`, plus Hangfire's own tables get created automatically on first run).

3. **Deploy the stored procedures.**
   Run `Database/StoredProcedures.sql` against the `Timesheet` database (in SSMS/Azure Data
   Studio, or `sqlcmd -S localhost\SQLEXPRESS -d Timesheet -i Database/StoredProcedures.sql`).
   It's safe to re-run — everything uses `CREATE OR ALTER`.

4. **Run it.**
   ```bash
   dotnet run
   ```
   On first run, `SeedData.cs` seeds four test accounts and three host companies
   automatically — no manual data entry needed.

## Walking the flow

Open the app and you'll land on the login screen. The seeded accounts:

| Name | Role | Notes |
|---|---|---|
| Alice Ngwenya | Employee | reports to Carol |
| Bob Dlamini | Employee | reports to Carol |
| Eve Sithole | Employee | already sitting in "pending admin approval" status |
| Carol Manager | Manager | sees Alice/Bob/Eve on her Manager Board |

A good path to trace the employee → manager flow:

1. **Log in as Alice.** Go to *Capture Your Timesheet*, fill in a date this week, host
   company, start/end time, and a task, then save. Watch `TimeTrackerController.Create`
   (POST) run through its validation (duplicate-date check, week-boundary check,
   missed-previous-week check) before it inserts the row.
2. **Log out, log in as Carol.** Go to *Manager Board*. This calls the
   `GetManagerEmployeeTimeSheetsPaged` stored procedure, filtered by
   `SupervisorEmail = carol.manager@local.test`, and lists Alice as an employee with a
   timesheet count. Click into Alice to see her individual entries
   (`GetEmployeeTimesheetPaged`), and try *Generate Report* to see the iTextSharp PDF export.
3. **Approve Employees.** Still as Carol, open *Approve Employees* — Eve is already there
   (seeded with `ApprovalStatus = 0`). Approve or reject her; rejecting schedules a Hangfire
   job (`ResetUserStatus`) to flip her back to normal status 24 hours later. Both actions try
   to send an email via `EmailService`; since `EmailSettings` in `appsettings.json` has no
   real SMTP credentials, the send will fail gracefully and just show a "but the email
   notification failed to send" message — the approval/rejection itself still goes through.
4. **Host Companies.** CRUD screen for the dropdown options employees pick from when
   capturing time — try adding one, then log back in as an employee and see it appear.

## If you want email notifications to actually send

Don't put real credentials in `appsettings.json` — set them via `dotnet user-secrets` instead, so
they never end up committed:

```bash
cd PSS_Time_Tracker
dotnet user-secrets set "EmailSettings:ServiceAccountEmail" "you@example.com"
dotnet user-secrets set "EmailSettings:ServiceAccountPassword" "your-app-password"
```

ASP.NET Core loads user secrets automatically in Development (the project already has a
`UserSecretsId`), and they override the blank values in `appsettings.json`. An app password works
well for most providers. Nothing in the code needs to change — `EmailService.cs` is untouched from
the original. For a shared/staging deployment, use environment variables or a real secrets manager
instead of user secrets (which are local-machine-only by design).

## Adding more test accounts

Edit `SeedData.cs` and add another `UserAccount` to the list (set `IsManager = true`/`false`
and point `SupervisorEmail` at an existing manager's email). Delete the `Users` table rows (or
the whole `Timesheet` database and re-run `dotnet ef database update`) to reseed, since seeding
only runs when the `Users` table is empty.
