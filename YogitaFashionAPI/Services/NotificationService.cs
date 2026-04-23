using System.Net;
using System.Net.Mail;

namespace YogitaFashionAPI.Services
{
    public interface INotificationService
    {
        Task<bool> SendEmailAsync(string recipientEmail, string subject, string body);
        string GetStoreFrontBaseUrl();
        string GetAdminPanelBaseUrl();
    }

    public class NotificationService : INotificationService
    {
        private const string DefaultFrontendBaseUrl = "http://127.0.0.1:5173";
        private readonly IConfiguration _configuration;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(IConfiguration configuration, ILogger<NotificationService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendEmailAsync(string recipientEmail, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                return false;
            }

            var simulate = _configuration.GetValue<bool>("AvailabilityAlerts:Simulate");
            var senderEmail = _configuration["AvailabilityAlerts:Email:SenderEmail"] ?? "patilpiyush1788@gmail.com";
            var senderName = _configuration["AvailabilityAlerts:Email:SenderName"] ?? "Yogita Fashion";

            if (simulate)
            {
                _logger.LogInformation("SIMULATED EMAIL from {Sender} to {Receiver}: {Subject}", senderEmail, recipientEmail, subject);
                return true;
            }

            var smtpHost = _configuration["AvailabilityAlerts:Email:SmtpHost"] ?? "smtp.gmail.com";
            var smtpPort = _configuration.GetValue<int?>("AvailabilityAlerts:Email:SmtpPort") ?? 587;
            var appPassword = _configuration["AvailabilityAlerts:Email:AppPassword"] ?? "";
            var useSsl = _configuration.GetValue<bool?>("AvailabilityAlerts:Email:UseSsl") ?? true;

            if (string.IsNullOrWhiteSpace(appPassword))
            {
                _logger.LogWarning("Email app password is missing. Cannot send email to {Receiver}.", recipientEmail);
                return false;
            }

            try
            {
                using var message = new MailMessage(new MailAddress(senderEmail, senderName), new MailAddress(recipientEmail))
                {
                    Subject = subject,
                    Body = body
                };

                using var smtp = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = useSsl,
                    Credentials = new NetworkCredential(senderEmail, appPassword)
                };

                await smtp.SendMailAsync(message);
                return true;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to send email to {Receiver}.", recipientEmail);
                return false;
            }
        }

        public string GetStoreFrontBaseUrl()
        {
            return NormalizeBaseUrl(
                _configuration["Frontend:StoreBaseUrl"] ??
                _configuration["Frontend:BaseUrl"] ??
                DefaultFrontendBaseUrl);
        }

        public string GetAdminPanelBaseUrl()
        {
            return NormalizeBaseUrl(
                _configuration["Frontend:AdminBaseUrl"] ??
                _configuration["Frontend:BaseUrl"] ??
                DefaultFrontendBaseUrl);
        }

        private static string NormalizeBaseUrl(string rawUrl)
        {
            var candidate = (rawUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                candidate = DefaultFrontendBaseUrl;
            }

            if (!candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                candidate = $"https://{candidate}";
            }

            return candidate.TrimEnd('/');
        }
    }
}
