using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using YogitaFashionAPI.Data;
using YogitaFashionAPI.Models;
using YogitaFashionAPI.Services;

namespace YogitaFashionAPI.Controllers
{
    [Route("support")]
    [ApiController]
    public class SupportController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly INotificationService _notificationService;
        private readonly AppDbContext _db;
        private readonly IAuditLogService _auditLogService;

        public SupportController(
            IConfiguration configuration,
            INotificationService notificationService,
            AppDbContext db,
            IAuditLogService auditLogService)
        {
            _configuration = configuration;
            _notificationService = notificationService;
            _db = db;
            _auditLogService = auditLogService;
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

        [HttpGet("requests/my")]
        [Authorize]
        public async Task<IActionResult> GetMyRequests()
        {
            var requesterId = GetCurrentUserId();
            if (!requesterId.HasValue)
            {
                return Unauthorized();
            }

            var requesterEmail = (User.FindFirstValue(ClaimTypes.Email) ?? "").Trim().ToLowerInvariant();
            var requesterPhone = NormalizePhone(User.FindFirstValue("phone") ?? "");

            var items = await _db.SupportRequests
                .Where(item =>
                    item.UserId == requesterId.Value ||
                    (!string.IsNullOrWhiteSpace(requesterEmail) && (item.Email ?? "").ToLower() == requesterEmail) ||
                    (!string.IsNullOrWhiteSpace(requesterPhone) && (item.Phone ?? "") == requesterPhone))
                .OrderByDescending(item => item.CreatedAt)
                .ToListAsync();
            return Ok(items);
        }

        [HttpPost("requests")]
        [AllowAnonymous]
        public async Task<IActionResult> CreateRequest([FromBody] SupportRequest input)
        {
            if (input == null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "SupportRequestCreate", "Support", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "missing_payload"
                });
                return BadRequest(new { message = "Support request payload is required." });
            }

            if (string.IsNullOrWhiteSpace(input.Message))
            {
                await _auditLogService.WriteAsync(User, HttpContext, "SupportRequestCreate", "Support", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "missing_message"
                });
                return BadRequest(new { message = "Support message is required." });
            }

            var requesterId = GetCurrentUserId();
            var requesterEmail = (User.FindFirstValue(ClaimTypes.Email) ?? "").Trim().ToLowerInvariant();
            if (!requesterId.HasValue &&
                string.IsNullOrWhiteSpace(input.Contact) &&
                string.IsNullOrWhiteSpace(input.Email) &&
                string.IsNullOrWhiteSpace(input.Phone))
            {
                await _auditLogService.WriteAsync(User, HttpContext, "SupportRequestCreate", "Support", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "missing_contact_details"
                });
                return BadRequest(new { message = "Contact details are required." });
            }

            var now = DateTime.UtcNow;
            var orderReference = (input.OrderId ?? input.OrderReference ?? "").Trim();
            var resolvedOrderId = await ResolveOrderDbIdAsync(orderReference);
            var fallbackUserId = input.UserId.GetValueOrDefault();
            var request = new SupportRequest
            {
                UserId = requesterId ?? (fallbackUserId > 0 ? fallbackUserId : null),
                Name = (input.Name ?? "").Trim(),
                Contact = (input.Contact ?? "").Trim(),
                Subject = string.IsNullOrWhiteSpace(input.Subject) ? "General Support" : input.Subject.Trim(),
                OrderReference = orderReference,
                OrderDbId = resolvedOrderId,
                Message = (input.Message ?? "").Trim(),
                Email = ResolveEmail(input.Email, requesterEmail, input.Contact ?? ""),
                Phone = ResolvePhone(input.Phone, input.Contact ?? ""),
                Status = "Open",
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.SupportRequests.Add(request);
            await _db.SaveChangesAsync();
            await _auditLogService.WriteAsync(User, HttpContext, "SupportRequestCreate", "Support", request.Id.ToString(), "Success", new Dictionary<string, object?>
            {
                ["subject"] = request.Subject,
                ["status"] = request.Status,
                ["orderReference"] = request.OrderId
            }, request.UserId.GetValueOrDefault() > 0 ? request.UserId : null);
            await SendSupportRequestEmail(request);

            return Ok(request);
        }

        private async Task SendSupportRequestEmail(SupportRequest request)
        {
            var receiverEmail = _configuration["SupportAlerts:ReceiverEmail"] ?? "patilpiyush1619@gmail.com";

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

            await _notificationService.SendEmailAsync(receiverEmail, subject, body);
        }

        private static string ResolveEmail(string email, string requesterEmail, string contact)
        {
            var normalizedEmail = (email ?? "").Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(normalizedEmail))
            {
                return normalizedEmail;
            }

            if (!string.IsNullOrWhiteSpace(requesterEmail))
            {
                return requesterEmail;
            }

            var fallbackContact = (contact ?? "").Trim();
            return fallbackContact.Contains('@') ? fallbackContact.ToLowerInvariant() : string.Empty;
        }

        private static string ResolvePhone(string phone, string contact)
        {
            var normalizedPhone = NormalizePhone(phone);
            if (!string.IsNullOrWhiteSpace(normalizedPhone))
            {
                return normalizedPhone;
            }

            return NormalizePhone(contact);
        }

        private static string NormalizePhone(string value)
        {
            return new string((value ?? "").Where(char.IsDigit).ToArray());
        }

        private async Task<int?> ResolveOrderDbIdAsync(string orderReference)
        {
            var normalized = (orderReference ?? "").Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            if (int.TryParse(normalized, out var numericOrderId))
            {
                var exists = await _db.Orders.AnyAsync(item => item.Id == numericOrderId);
                return exists ? numericOrderId : null;
            }

            var upperReference = normalized.ToUpperInvariant();
            var order = await _db.Orders
                .AsNoTracking()
                .Where(item => (item.OrderNumber ?? "").ToUpper() == upperReference)
                .Select(item => new { item.Id })
                .FirstOrDefaultAsync();

            return order?.Id;
        }

        private int? GetCurrentUserId()
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(idClaim, out var parsedId) ? parsedId : null;
        }
    }
}
