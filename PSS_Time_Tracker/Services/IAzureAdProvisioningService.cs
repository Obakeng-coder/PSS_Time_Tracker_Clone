using PSS_Time_Tracker.Models;

namespace PSS_Time_Tracker.Services
{
    /// <summary>
    /// Looks a not-yet-seen sign-in up directly in the tenant directory and builds the UserAccount row
    /// this app would provision for them - Option 3 from the "will every worker need to be added by
    /// hand" discussion: since Azure AD already has every real employee, a real person Azure AD just
    /// authenticated shouldn't need a human to separately create their row before they can use the app.
    /// Only ever fills in a plain-employee profile (IsManager/IsHr false) - elevating someone to
    /// manager/HR is still a deliberate, separate decision, not something the directory lookup grants on
    /// its own.
    /// </summary>
    public interface IAzureAdProvisioningService
    {
        /// <summary>Null if this email doesn't resolve to a real user in the tenant directory (or the
        /// lookup itself failed/isn't configured) - the caller should refuse sign-in in that case, same
        /// as today, rather than provisioning a row it has no real profile data for.</summary>
        Task<UserAccount?> TryBuildProfileAsync(string email);
    }
}
