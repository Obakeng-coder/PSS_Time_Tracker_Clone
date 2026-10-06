using System.Net.Http.Json;

namespace PSS_Time_Tracker.Services
{
    /// <inheritdoc cref="ITeamsNotificationService"/>
    public class TeamsNotificationService : ITeamsNotificationService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TeamsNotificationService> _logger;

        public TeamsNotificationService(HttpClient httpClient, IConfiguration configuration, ILogger<TeamsNotificationService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendAsync(string recipientEmail, string title, string? message, string? relativeUrl)
        {
            // The flow URL embeds its own access signature, so it's a secret - set it outside the repo.
            var flowUrl = _configuration["Teams:FlowUrl"];
            if (string.IsNullOrWhiteSpace(flowUrl) || string.IsNullOrWhiteSpace(recipientEmail))
            {
                return;
            }

            var baseUrl = (_configuration["Teams:AppBaseUrl"] ?? "").TrimEnd('/');
            var link = !string.IsNullOrWhiteSpace(relativeUrl) && relativeUrl.StartsWith('/') && baseUrl.Length > 0
                ? baseUrl + relativeUrl
                : "";

            try
            {
                using var response = await _httpClient.PostAsJsonAsync(flowUrl, new
                {
                    recipientEmail,
                    title,
                    message = message ?? "",
                    link
                });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Teams flow rejected the notification for {Email}: {Status}", recipientEmail, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Couldn't send the Teams notification for {Email}", recipientEmail);
            }
        }
    }
}
