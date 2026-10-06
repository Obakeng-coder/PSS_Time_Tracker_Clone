using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using PSS_Time_Tracker.Services;

namespace PSS_Time_Tracker
{
    /// <summary>
    /// Sends the system's emails. Two interchangeable ways, picked by configuration:
    ///   - SMTP (EmailSettings:SmtpServer set) - free with any mailbox that allows SMTP, e.g. a Gmail
    ///     account with an app password, or Microsoft 365 if SMTP AUTH is enabled for the mailbox.
    ///   - Microsoft Graph sendMail (no SmtpServer) - uses the sign-in app registration's secret and the
    ///     Graph "Mail.Send" application permission; no mailbox password at all.
    /// Callers never need to care which: <see cref="SendAsync"/> throws a clear error if neither is set up.
    /// </summary>
    public class EmailService : MicrosoftGraphServiceBase
    {
        private readonly ILogger<EmailService> _logger;

        public EmailService(HttpClient httpClient, IConfiguration configuration, ILogger<EmailService> logger)
            : base(httpClient, configuration)
        {
            _logger = logger;
        }

        protected override string ConfigSectionName => "AzureAd";

        private string? SmtpServer => Configuration["EmailSettings:SmtpServer"];

        private string? SenderAddress =>
            !string.IsNullOrWhiteSpace(Configuration["EmailSettings:SenderAddress"])
                ? Configuration["EmailSettings:SenderAddress"]
                : Configuration["EmailSettings:Username"];

        private string FromName => Configuration["EmailSettings:FromName"] ?? "HourTrack";

        public bool IsEmailConfigured() =>
            !string.IsNullOrWhiteSpace(SenderAddress) &&
            (!string.IsNullOrWhiteSpace(SmtpServer)
                ? !string.IsNullOrWhiteSpace(Configuration["EmailSettings:Password"])
                : IsConfigured());

        public async Task SendAsync(string toEmail, string subject, string body)
        {
            if (!IsEmailConfigured())
            {
                throw new InvalidOperationException(
                    "Email isn't set up: configure EmailSettings (SmtpServer/Username/Password/SenderAddress) " +
                    "or the Graph Mail.Send route (SenderAddress + AzureAd client secret).");
            }

            if (!string.IsNullOrWhiteSpace(SmtpServer))
            {
                await SendViaSmtpAsync(toEmail, subject, body);
            }
            else
            {
                await SendViaGraphAsync(toEmail, subject, body);
            }
        }

        private async Task SendViaSmtpAsync(string toEmail, string subject, string body)
        {
            using var client = new SmtpClient(SmtpServer, int.Parse(Configuration["EmailSettings:SmtpPort"] ?? "587"))
            {
                Credentials = new NetworkCredential(
                    Configuration["EmailSettings:Username"] ?? SenderAddress,
                    Configuration["EmailSettings:Password"]),
                EnableSsl = bool.Parse(Configuration["EmailSettings:EnableSsl"] ?? "true"),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 15000
            };

            using var message = new MailMessage(new MailAddress(SenderAddress!, FromName), new MailAddress(toEmail))
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };

            await client.SendMailAsync(message);
        }

        private async Task SendViaGraphAsync(string toEmail, string subject, string body)
        {
            var payload = new
            {
                message = new
                {
                    subject,
                    body = new { contentType = "Text", content = body },
                    toRecipients = new[] { new { emailAddress = new { address = toEmail } } }
                },
                saveToSentItems = false
            };

            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"{GraphBaseUrl}/users/{Uri.EscapeDataString(SenderAddress!)}/sendMail")
            {
                Content = JsonContent.Create(payload)
            };

            using var response = await SendAuthenticatedAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync();
                _logger.LogError("Graph sendMail to {To} failed: {Status} {Detail}", toEmail, response.StatusCode, detail);
                throw new HttpRequestException($"Graph sendMail failed with {(int)response.StatusCode}: {detail}");
            }
        }
    }
}
