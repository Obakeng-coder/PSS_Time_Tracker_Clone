using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace PSS_Time_Tracker
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

// --------------------------------------------------------------------------------------------------------------------------------
        public async Task SendApprovalEmailAsync(
            string toEmail,
            string employeeName,
            string adminName,
            string adminEmail)
        {
            var emailSettings = _configuration.GetSection("EmailSettings");

            using var smtpClient = new SmtpClient(emailSettings["SmtpServer"])
            {
                Port = int.Parse(emailSettings["SmtpPort"]),
                Credentials = new NetworkCredential(
                    emailSettings["ServiceAccountEmail"],
                    emailSettings["ServiceAccountPassword"]),
                EnableSsl = bool.Parse(emailSettings["EnableSsl"] ?? "true"),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 10000 // 10 seconds timeout
            };

            var fromEmail = new MailAddress(
                emailSettings["ServiceAccountEmail"],
                $"{emailSettings["FromName"]}");

            using var mailMessage = new MailMessage(fromEmail, new MailAddress(toEmail))
            {
                Subject = "Timesheet Access Approved",
                Body = $"Dear {employeeName},\n\n" +
                       $"Your timesheet access has been approved by {adminName} ({adminEmail}). " +
                       "You can now create and submit timesheets in the system.\n\n" +
                       "If you have any questions, please contact your line manager.\n\n" +
                       "Regards,\n" +
                       "Timesheet System Team",
                IsBodyHtml = false,
                ReplyTo = new MailAddress(adminEmail)
            };

            await smtpClient.SendMailAsync(mailMessage);
        }

// --------------------------------------------------------------------------------------------------------------------------------
        public async Task SendRejectionEmailAsync(
            string toEmail,
            string employeeName,
            string adminName,
            string adminEmail)
        {
            var emailSettings = _configuration.GetSection("EmailSettings");

            using var smtpClient = new SmtpClient(emailSettings["SmtpServer"])
            {
                Port = int.Parse(emailSettings["SmtpPort"]),
                Credentials = new NetworkCredential(
                    emailSettings["ServiceAccountEmail"],
                    emailSettings["ServiceAccountPassword"]),
                EnableSsl = bool.Parse(emailSettings["EnableSsl"] ?? "true"),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 10000 // 10 seconds timeout
            };

            var fromEmail = new MailAddress(
                emailSettings["ServiceAccountEmail"],
                $"{emailSettings["FromName"]}");

            using var mailMessage = new MailMessage(fromEmail, new MailAddress(toEmail))
            {
                Subject = "Timesheet Access Request Rejected",
                Body = $"Dear {employeeName},\n\n" +
                       $"Your timesheet access request has been reviewed by {adminName} ({adminEmail}) " +
                       "and unfortunately we cannot approve your request at this time.\n\n" +
                       "If you believe this is an error or would like more information, " +
                       $"please contact {adminName} directly at {adminEmail}.\n\n" +
                       "Regards,\n" +
                       "Timesheet System Team",
                IsBodyHtml = false,
                ReplyTo = new MailAddress(adminEmail)
            };

            await smtpClient.SendMailAsync(mailMessage);
        }

// --------------------------------------------------------------------------------------------------------------------------------
        public async Task SendReapplyNotificationEmailAsync(
        string toEmail,
        string employeeName)
        {
            var emailSettings = _configuration.GetSection("EmailSettings");

            using var smtpClient = new SmtpClient(emailSettings["SmtpServer"])
            {
                Port = int.Parse(emailSettings["SmtpPort"]),
                Credentials = new NetworkCredential(
                    emailSettings["ServiceAccountEmail"],
                    emailSettings["ServiceAccountPassword"]),
                EnableSsl = bool.Parse(emailSettings["EnableSsl"] ?? "true"),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 10000
            };

            var fromEmail = new MailAddress(
                emailSettings["ServiceAccountEmail"],
                $"{emailSettings["FromName"]}");

            using var mailMessage = new MailMessage(fromEmail, new MailAddress(toEmail))
            {
                Subject = "You Can Reapply for Timesheet Access",
                Body = $"Dear {employeeName},\n\n" +
                       "Your waiting period following a rejected access request has now ended.\n" +
                       "You may now reapply for timesheet system access by submitting a new request.\n\n" +
                       "Regards,\n" +
                       "Timesheet System Team",
                IsBodyHtml = false
            };

            await smtpClient.SendMailAsync(mailMessage);
        }
 // --------------------------------------------------------------------------------------------------------------------------------
        public async Task SendAdminApprovalRequestToManagerAsync(
    string managerEmail,
    string managerName,
    string employeeName,
    string employeeEmail)
        {
            var emailSettings = _configuration.GetSection("EmailSettings");

            using var smtpClient = new SmtpClient(emailSettings["SmtpServer"])
            {
                Port = int.Parse(emailSettings["SmtpPort"]),
                Credentials = new NetworkCredential(
                    emailSettings["ServiceAccountEmail"],
                    emailSettings["ServiceAccountPassword"]),
                EnableSsl = bool.Parse(emailSettings["EnableSsl"] ?? "true"),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 10000
            };

            var fromEmail = new MailAddress(
                emailSettings["ServiceAccountEmail"],
                $"{emailSettings["FromName"]}");

            using var mailMessage = new MailMessage(fromEmail, new MailAddress(managerEmail))
            {
                Subject = "Employee Admin Approval Request",
                Body = $"Dear {managerName},\n\n" +
                       $"Employee {employeeName} ({employeeEmail}) has requested admin approval for timesheet access.\n\n" +
                       "Please review and process this request in the admin portal.\n\n" +
                       "Regards,\n" +
                       "Timesheet System Team",
                IsBodyHtml = false,
                ReplyTo = new MailAddress(employeeEmail)
            };

            await smtpClient.SendMailAsync(mailMessage);
        }

    }
}
// --------------------------------------------------------------------------------------------------------------------------------

