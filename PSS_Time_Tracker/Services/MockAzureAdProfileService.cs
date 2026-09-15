namespace PSS_Time_Tracker.Services
{
    /// <summary>
    /// Realistic stand-in for <see cref="AzureAdProfileService"/> - see MockSharePointCheckInService
    /// for why this exists and the "(Mock)" labelling convention.
    /// </summary>
    public class MockAzureAdProfileService : IAzureAdProfileService
    {
        private static readonly string[] Departments = { "Engineering", "Finance", "Operations", "People & Culture" };

        public Task<AzureAdProfile?> GetProfileAsync(string userPrincipalName)
        {
            var seed = Math.Abs(userPrincipalName.GetHashCode());
            return Task.FromResult<AzureAdProfile?>(new AzureAdProfile
            {
                JobTitle = "(Mock) Software Engineer",
                Department = $"(Mock) {Departments[seed % Departments.Length]}"
            });
        }
    }
}
