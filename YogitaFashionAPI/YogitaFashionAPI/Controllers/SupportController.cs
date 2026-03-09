using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using YogitaFashionAPI.Data;
using YogitaFashionAPI.Models;

namespace YogitaFashionAPI.Controllers
{
    [Route("support")]
    [ApiController]
    public class SupportController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SupportController> _logger;
        private readonly AppDbContext _db;

        public SupportController(IConfiguration configuration, ILogger<SupportController> logger, AppDbContext db)
        {
            _configuration = configuration;
            _logger = logger;
            _db = db;
        }

        [HttpGet("requests")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetRequests()
        {
            var items = await _db.SupportRequests
                .OrderByDescending(item => item.CreatedAt)
                .ToListAsync();
            return Ok(items);
        }

        [HttpPost("requests")]
        [AllowAnonymous]
        public async Task<IActionResult> CreateRequest([FromBody] SupportRequest input)
        {
            var now = DateTime.UtcNow;
            var request = new SupportRequest
            {
                UserId = input.UserId,
                Name = (input.Name ?? "").Trim(),
                Contact = (input.Contact ?? "").Trim(),
                Subject = string.IsNullOrWhiteSpace(input.Subject) ? "General Support" : input.Subject.Trim(),
                OrderId = (input.OrderId ?? "").Trim(),
                Message = (input.Message ?? "").Trim(),
                Email = (input.Email ?? "").Trim().ToLowerInvariant(),
                Phone = (input.Phone ?? "").Trim(),
                Status = string.IsNullOrWhiteSpace(input.Status) ? "Open" : input.Status.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.SupportRequests.Add(request);
            await _db.SaveChangesAsync();
            await SendSupportRequestEmail(request);

            return Ok(request);
        }

        private async Task SendSupportRequestEmail(SupportRequest request)
        {
            var senderEmail = _configuration["AvailabilityAlerts:Email:SenderEmail"] ?? "patilpiyush1788@gmail.com";
            var senderName = _configuration["AvailabilityAlerts:Email:SenderName"] ?? "Yogita Fashion";
            var receiverEmail = _configuration["SupportAlerts:ReceiverEmail"] ?? "patilpiyush1619@gmail.com";
            var simulate = _configuration.GetValue<bool>("AvailabilityAlerts:Simulate");

            var subject = $"New Support Request: {request.Subject} ({request.Id})";
            var body =
                $"Support request received.\n\n" +
                $"Ticket: {request.Id}\n" +
                $"Name: {request.Name}\n" +
                $"Contact: {request.Contact}\n" +
                $"Email: {request.Email}\n" +
                $"Phone: {request.Phone}\n" +
                $"Order ID: {request.OrderId}\n" +
                $"Subject: {request.Subject}\n" +
                $"Message:\n{request.Message}\n\n" +
                $"Created: {request.CreatedAt:u}";

            if (simulate)
            {
                _logger.LogInformation("SIMULATED SUPPORT EMAIL from {Sender} to {Receiver}: {Subject}", senderEmail, receiverEmail, subject);
                await Task.CompletedTask;
                return;
            }

            var smtpHost = _configuration["AvailabilityAlerts:Email:SmtpHost"] ?? "smtp.gmail.com";
            var smtpPort = _configuration.GetValue<int?>("AvailabilityAlerts:Email:SmtpPort") ?? 587;
            var appPassword = _configuration["AvailabilityAlerts:Email:AppPassword"] ?? "";
            var useSsl = _configuration.GetValue<bool?>("AvailabilityAlerts:Email:UseSsl") ?? true;

            if (string.IsNullOrWhiteSpace(appPassword))
            {
                _logger.LogWarning("Email app password is missing. Could not send support email for ticket {TicketId}.", request.Id);
                return;
            }

            try
            {
                using var message = new MailMessage(new MailAddress(senderEmail, senderName), new MailAddress(receiverEmail))
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
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to send support email for ticket {TicketId}.", request.Id);
            }
        }
    }
}
