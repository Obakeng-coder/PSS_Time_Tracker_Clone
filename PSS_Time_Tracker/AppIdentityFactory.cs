using System.Security.Claims;
using PSS_Time_Tracker.Models;

namespace PSS_Time_Tracker
{
    /// <summary>
    /// Builds the app's own claims identity from a local <see cref="UserAccount"/> row - the single
    /// place both sign-in paths agree on what a signed-in user "looks like" downstream (every
    /// [Authorize]/RequireManagerRole/RequireHrRole check, User.GetUserId(), the Views that read the
    /// "name" claim). AccountController's mock dropdown login builds this directly from the account the
    /// tester picked; Program.cs's real Azure AD sign-in (OnTokenValidated) builds the exact same shape
    /// after looking that account up by the email Azure AD authenticated - so which door someone came in
    /// through is invisible to the rest of the app. Roles are never read from Azure AD groups or App
    /// Roles; IsManager/IsHr on the local row is the only source of truth for what someone can do, same
    /// as the "everyone must already be in the database" rule for who can sign in at all.
    /// </summary>
    public static class AppIdentityFactory
    {
        public static ClaimsIdentity BuildIdentity(UserAccount user, IConfiguration configuration, string authenticationType)
        {
            // Note: the claim type is the literal string "name" (not ClaimTypes.Name's long URI)
            // because that's what the existing Views check for (c.Type == "name"), matching the raw
            // "name" claim Azure AD's OIDC token used to carry.
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.AzureAdUserId),
                new Claim("name", $"{user.EmployeeName} {user.EmployeeSurname}"),
                new Claim("preferred_username", user.Email),
                new Claim("email", user.Email),
            };

            if (user.IsManager)
            {
                claims.Add(new Claim("groups", configuration["ManagerGroupId"] ?? "local-managers"));
            }

            if (user.IsHr)
            {
                claims.Add(new Claim("groups", configuration["HrGroupId"] ?? "local-hr"));
            }

            return new ClaimsIdentity(claims, authenticationType, nameType: "name", roleType: ClaimTypes.Role);
        }
    }
}
