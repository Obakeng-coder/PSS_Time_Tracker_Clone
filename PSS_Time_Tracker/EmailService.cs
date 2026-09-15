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
 // --------------------------------------------------------------------------------------------------------------------------------
        public async Task SendLeaveRequestToManagerAsync(
            string managerEmail,
            string managerName,
            string employeeName,
            string leaveTypeName,
            DateTime startDate,
            DateTime endDate)
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
                Subject = "Leave Request Awaiting Your Recommendation",
                Body = $"Dear {managerName},\n\n" +
                       $"{employeeName} has requested {leaveTypeName} leave from {startDate:yyyy-MM-dd} " +
                       $"to {endDate:yyyy-MM-dd}.\n\n" +
                       "Please review and record your recommendation in the Approve Time Off screen.\n\n" +
                       "Regards,\n" +
                       "Timesheet System Team",
                IsBodyHtml = false
            };

            await smtpClient.SendMailAsync(mailMessage);
        }
 // --------------------------------------------------------------------------------------------------------------------------------
        public async Task SendLeaveDecisionEmailAsync(
            string toEmail,
            string employeeName,
            string stageName,
            string decisionSummary)
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
                Subject = $"Leave Request Update: {stageName}",
                Body = $"Dear {employeeName},\n\n" +
                       $"There's an update on your leave request at the {stageName} stage:\n\n" +
                       $"{decisionSummary}\n\n" +
                       "Regards,\n" +
                       "Timesheet System Team",
                IsBodyHtml = false
            };

            await smtpClient.SendMailAsync(mailMessage);
        }
 // --------------------------------------------------------------------------------------------------------------------------------
        public async Task SendLeaveRequestToHrAsync(
            string hrEmail,
            string hrName,
            string employeeName,
            string leaveTypeName,
            DateTime startDate,
            DateTime endDate)
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

            using var mailMessage = new MailMessage(fromEmail, new MailAddress(hrEmail))
            {
                Subject = "Leave Request Awaiting HR Decision",
                Body = $"Dear {hrName},\n\n" +
                       $"{employeeName}'s {leaveTypeName} leave request ({startDate:yyyy-MM-dd} to " +
                       $"{endDate:yyyy-MM-dd}) has been recommended by their manager and now needs an " +
                       "HR decision (with pay / without pay / not approved).\n\n" +
                       "Please review it on the HR Board.\n\n" +
                       "Regards,\n" +
                       "Timesheet System Team",
                IsBodyHtml = false
            };

            await smtpClient.SendMailAsync(mailMessage);
        }

 // --------------------------------------------------------------------------------------------------------------------------------
        public async Task SendTimesheetGapNotificationAsync(
            string toEmail,
            string employeeName,
            string gapSummary)
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
                Subject = "Timesheet Gaps Need Your Attention",
                Body = $"Dear {employeeName},\n\n" +
                       $"{gapSummary}\n\n" +
                       "Regards,\n" +
                       "Timesheet System Team",
                IsBodyHtml = false
            };

            await smtpClient.SendMailAsync(mailMessage);
        }

    }
}
// --------------------------------------------------------------------------------------------------------------------------------

