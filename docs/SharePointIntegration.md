# SharePoint & Azure AD Integration

Power Apps is no longer part of this - clock-in/out and leave data come from SharePoint, and employee
profile data (Job Title, Department) comes from Azure AD. Both reuse the same Entra ID app
registration and Graph token.

## Testing right now, with no Azure/SharePoint credentials at all

`appsettings.Development.json` sets `SharePointIntegration:UseMockData` to `true`, which swaps in
`MockSharePointCheckInService`, `MockSharePointLeaveBalanceService`, and `MockAzureAdProfileService`
in place of the real Graph-backed ones - `dotnet run` already uses these out of the box. Every value
they return is prefixed **"(Mock)"** so it's never mistaken for real data:

- **Capture Your Timesheet** gets a mock check-in for *today only* (so testing the "no check-in yet"
  path just means picking a different date), a Work Location dropdown, and a mock "Reports to"/Job
  Title.
- **Request Time Off** / **My Time Off** get realistic-looking (but made-up, deterministic per
  employee name) leave balances.

**Once real credentials are configured** (steps below), set `UseMockData` to `false` (or remove it -
it only exists in `appsettings.Development.json`, so Production is never affected) to switch to the
real services.

## Azure AD profile data (Job Title / Department)

`IAzureAdProfileService` looks up the signed-in employee's real Azure AD profile via Graph
(`GET /users/{upn}?$select=jobTitle,department`), closer to how the original (pre-clone) app worked.
Used on **Capture Your Timesheet** (Job Title) and **Request Time Off** (Department) - both fall back
to the local `UserAccount` field if no matching Azure AD user is found (which is always the case for
the local seeded test accounts, since they use `@local.test` addresses).

**Needs one more Graph permission on the same app registration**: **`User.Read.All`** (Application
permission, with admin consent) - in addition to the `Sites.Read.All`/`Sites.Selected` permission
below. Same app, same three config values (`TenantId`/`ClientId`/`ClientSecret`), just one more scope
granted to it.

# SharePoint Check-In Integration

Pulls Employee Name, Check-In/Check-Out time, Work Location, and the "Reports to" manager live from
the **MobileCheckIn** SharePoint list on every Capture Your Timesheet page load/submit, instead of
the employee typing them. The employee only edits the **Date** and **Work Location**.

Site: `https://providencesoft.sharepoint.com/sites/ProvidenceInternal`, list: `MobileCheckIn`.

## Confirmed list columns (from the real list)

| Display name | Example value | Used for |
|---|---|---|
| Title | "Nomvelo Zulu Zulu" | Employee name - also the join key, since there's no separate email column |
| Date | 1/3/2026 | Which day's row this is |
| CheckIn | 05:29 | Time-of-day text, combined with Date |
| CheckOut | 10:33 | Time-of-day text, combined with Date |
| Work Location | "Work From Home", "PSS 35" | Replaces the old Host Company dropdown |
| CheckInAddress | "Phumelani, Die Hoewes, Centurion, 0157" | Not currently used - Work Location is the field driving the app's dropdown |
| Total Hours Worked | "9 hours 20 minutes" | Not read - TymSheet computes this itself from CheckIn/CheckOut instead (the list's own version shows negative values for still-checked-in rows, so it's not reliable to trust directly) |
| Reports to | "Peter Bereta \| Providence Software ZA" | The employee's manager - **text, not an email or person field** |
| Status | "Checked Out" / "Checked In" | Not currently used |

## A second list: LeaveInformation (real leave balances)

Also pulls real leave allowances and used-so-far counts from the **LeaveInformation** list, replacing
the placeholder numbers TymSheet's own seed data used to invent. Confirmed columns:

| Display name | Example value | Used for |
|---|---|---|
| Title | "malk.mokgoshi@providencesoft.com" | Not used for matching (see below) |
| displayName | "Malk Mokgoshi \| Providence Soft ZA" | **Matched by name** - parsed for the part before "\|" |
| AnnualLeave / AnnualLeavesUsed | 15 / 9 | Annual leave remaining = AnnualLeave − AnnualLeavesUsed |
| SickLeave / SicksLeavesUsed | 30 / 3 | Same pattern |
| FamilyResponsibilityLeave / …Used | 3 / 0 | Same pattern |
| UnpaidLeaves | 0 | Shown but not capped - unpaid leave has no fixed allowance |
| MaternityLeave / PaternityLeave | "10 days", "4 Months" | Free text, inconsistent units across rows - shown as informational text on **My Time Off**, not turned into a day count |

Even though this list's `Title` column holds an email (unlike `MobileCheckIn`'s `Title`, which holds a
name), matching is done by **name** against `displayName` per your instruction - using
`startswith(fields/displayName, '<Employee Full Name>')` so the exact "| Company" suffix doesn't need
to match exactly.

**Known gap - no write-back:** `LeaveController` (balance display and the "does this request exceed
the remaining balance" check) reads this list live on every page load. `LeaveApprovalController`'s
Payroll-capture step still updates its own local `LeaveBalances` table when a request is completed,
but that local number is **no longer what's shown or checked** - only SharePoint's own numbers are.
So approving leave in TymSheet won't reduce what SharePoint shows until someone updates the
SharePoint list separately. If that's not acceptable, the fix is either (a) write the deduction back
to SharePoint via Graph on approval, or (b) keep balance tracking entirely local again. Flagging this
rather than guessing which you want.

## What's already built

- **`ISharePointCheckInService`** / **`SharePointCheckInService`** — calls Microsoft Graph with an
  app-only (client credentials) token, filters the list by `Title` (employee full name) + `Date`, and
  combines the `CheckIn`/`CheckOut` time text with `Date` into full timestamps.
- **`TimeTrackerController.Create`** — GET prefills Start/End Time and the Work Location dropdown from
  today's SharePoint record; POST always re-derives Start/End Time from SharePoint for the selected
  date (never trusts a client-submitted value), and blocks submission if no check-in/check-out record
  exists for that date yet (unless it's a Public Holiday). Total hours worked is computed by TymSheet
  itself (CheckOut − CheckIn), not read from the list's own "Total Hours Worked" column.
- **Manager sync** — "Reports to" is parsed for the name before the "|" (e.g. "Peter Bereta"), and used
  to update `UserAccount.SupervisorFullName`. It also tries to resolve an email by matching that name
  against an existing TymSheet account, so the existing Manager Board (which queries by
  `SupervisorEmail`) keeps working - see the caveat below.
- **Host Company removed entirely** — replaced by Work Location everywhere (Capture Timesheet, Track
  Your Time, Manager Board, the PDF report). The `HostCompanies` table has been dropped.
- **Dumisani Nkosi** added as a test account (`SeedData.cs`) matching a real employee in the list, so
  he's ready to test with once this is live.

All of the above is implemented and was verified end-to-end with a stand-in for SharePoint (used only
for testing, not shipped) — the full Capture Timesheet flow, hours computation, and manager sync all
confirmed working. What's left is pointing it at the real list.

## Known caveat: manager email resolution

Since "Reports to" only gives a name, not an email, TymSheet can only fill in
`UserAccount.SupervisorEmail` for a manager who **already has a TymSheet account** with a matching
name (e.g. Carol Manager). For managers who appear in "Reports to" but don't have a TymSheet account
yet (Peter Bereta, Sivan Moodley, Hlengiwe Lupungela, Reuben Modiba, etc., from the screenshots), their
direct reports' `SupervisorFullName` still updates correctly (so it displays right), but
`SupervisorEmail` stays unset — meaning that manager won't see those employees on their Manager Board
until they're added as a TymSheet account too. Add them via the same `SeedData.cs` pattern used for
Dumisani, or ask and I'll build a small "provision from Reports-to" admin action instead.

## What you (or your Power Platform/SharePoint admin) need to do

### 1. Register an Azure AD app for Graph access

Needs these **Application permissions** (not Delegated), each with tenant admin consent:
- `Sites.Read.All` — simplest, but grants read access to every SharePoint site in the tenant, or
  `Sites.Selected` scoped to just `ProvidenceInternal` instead — more restrictive, but needs an extra
  one-time Graph call from an admin to grant the app access to that specific site.
- `User.Read.All` — for the Azure AD profile lookups (Job Title/Department).

Then set:

```bash
cd PSS_Time_Tracker
dotnet user-secrets set "SharePointIntegration:TenantId" "<tenant-id>"
dotnet user-secrets set "SharePointIntegration:ClientId" "<app-registration-client-id>"
dotnet user-secrets set "SharePointIntegration:ClientSecret" "<app-registration-client-secret>"
```

(Same rule as the other integrations: environment variables or a real secrets manager for anything
beyond local dev — never `appsettings.json`.)

### 2. Confirm internal column names if the defaults don't match

SharePoint's **internal** column name (what Graph actually returns) isn't always the same as the
**display** name. `Title`, `CheckIn`, and `CheckOut` have no spaces so they're very likely identical
internally - `Work Location` and `Reports to` (both have spaces) are the two worth double-checking.
Override via config if needed:

```bash
dotnet user-secrets set "SharePointIntegration:Fields:WorkLocation" "<real internal name>"
dotnet user-secrets set "SharePointIntegration:Fields:ReportsTo" "<real internal name>"
```

To find the real internal name: **List Settings → click the column → the `Field=` value in the URL**
is the internal name.

The `LeaveInformation` list's columns follow the same rule - override any of these if the internal
names turn out to differ from the display names shown above (`AnnualLeave`, `SickLeave` etc. have no
spaces so are likely exact matches; `displayName` is already the internal name for a computed/synced
column so is very unlikely to need overriding):

```bash
dotnet user-secrets set "SharePointIntegration:LeaveListName" "LeaveInformation"
dotnet user-secrets set "SharePointIntegration:Fields:LeaveDisplayName" "<real internal name>"
```

### 3. Set the SharePoint location values (only if they differ from the defaults, already correct for this site)

```bash
dotnet user-secrets set "SharePointIntegration:SiteHostname" "providencesoft.sharepoint.com"
dotnet user-secrets set "SharePointIntegration:SitePath" "/sites/ProvidenceInternal"
dotnet user-secrets set "SharePointIntegration:ListName" "MobileCheckIn"
```

## Testing it end to end

1. Set the three credential values from step 1.
2. Log into TymSheet as an employee who has a matching row in the list (Dumisani Nkosi, or seed
   another one matching a name you can see in the list).
3. Open **Capture Your Timesheet** — Start Time, End Time, and the Work Location dropdown should
   populate from today's row. If nothing shows up, the column mapping in step 2 is the first thing to
   check (the service fails safe and just shows nothing rather than erroring, so a silent mismatch is
   the most likely first issue).
4. Submit the timesheet — it should save with the correct Work Location and computed hours.

## What this does *not* do (yet)

- It reads the *employee's own* row for the selected date - it doesn't yet show a manager a live feed
  of their team's check-ins on Manager Board.
- If an employee has more than one row for the same day (e.g. multiple check-in/out cycles), only the
  first one Graph returns is used.
- Managers who appear in "Reports to" but don't have a TymSheet account yet won't see their reports on
  Manager Board (see the caveat above).
