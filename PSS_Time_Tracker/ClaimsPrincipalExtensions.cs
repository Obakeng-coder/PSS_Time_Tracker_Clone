using System.Security.Claims;

namespace PSS_Time_Tracker
{
    public static class ClaimsPrincipalExtensions
    {
        public static bool IsManager(this ClaimsPrincipal user, string managerGroupId)
        {
            return user?.Claims.Any(c => c.Type == "groups" && c.Value == managerGroupId) ?? false;
        }

        // Replaces Microsoft.Identity.Web's ClaimsPrincipal.GetObjectId(), which used to read
        // the Azure AD "oid" claim. Our local login sets the standard NameIdentifier claim
        // to the same AzureAdUserId used as the primary key everywhere else in the app.
        public static string? GetUserId(this ClaimsPrincipal user)
        {
            return user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }
}
