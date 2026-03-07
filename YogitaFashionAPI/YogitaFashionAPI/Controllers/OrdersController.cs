using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YogitaFashionAPI.Models;
using YogitaFashionAPI.Services;

namespace YogitaFashionAPI.Controllers
{
    [Route("orders")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        public static List<Order> OrderStore => orders;

        private static readonly List<Order> orders = new();
        private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Pending",
            "Confirmed",
            "Packed",
            "Shipped",
            "Delivered",
            "Cancelled",
            "Returned",
            "Refunded",
            "Rejected"
        };

        private static readonly Dictionary<string, HashSet<string>> AllowedTransitions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Pending"] = new(StringComparer.OrdinalIgnoreCase) { "Confirmed", "Cancelled" },
            ["Confirmed"] = new(StringComparer.OrdinalIgnoreCase) { "Packed", "Cancelled" },
            ["Packed"] = new(StringComparer.OrdinalIgnoreCase) { "Shipped", "Cancelled" },
            ["Shipped"] = new(StringComparer.OrdinalIgnoreCase) { "Delivered", "Returned" },
            ["Delivered"] = new(StringComparer.OrdinalIgnoreCase) { "Returned" },
            ["Returned"] = new(StringComparer.OrdinalIgnoreCase) { "Refunded", "Rejected" },
            ["Cancelled"] = new(StringComparer.OrdinalIgnoreCase),
            ["Refunded"] = new(StringComparer.OrdinalIgnoreCase),
            ["Rejected"] = new(StringComparer.OrdinalIgnoreCase)
        };

        private readonly IConfiguration _configuration;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(IConfiguration configuration, ILogger<OrdersController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> CreateOrder(Order order)
        {
            order.Items ??= new List<OrderItem>();
            if (order.Items.Count == 0)
            {
                return BadRequest("Order items are required.");
            }

            var stockIssues = new List<object>();
            var productsToUpdate = new List<(Product product, int qty)>();

            foreach (var item in order.Items)
            {
                var rawProductId = (item.ProductId ?? "").Trim();
                if (!TryResolveProductId(rawProductId, out var productId))
                {
                    stockIssues.Add(new
                    {
                        productId = rawProductId,
                        item.Title,
                        message = "Invalid product id."
                    });
                    continue;
                }

                var product = ProductsController.ProductStore.FirstOrDefault(p => p.Id == productId);
                if (product == null)
                {
                    stockIssues.Add(new
                    {
                        productId,
                        item.Title,
                        message = "Product not found."
                    });
                    continue;
                }

                var qty = Math.Max(1, item.Qty);
                if (product.Stock < qty)
                {
                    stockIssues.Add(new
                    {
                        productId,
                        product.Name,
                        requested = qty,
                        available = product.Stock,
                        message = "Out of stock."
                    });
                    continue;
                }

                productsToUpdate.Add((product, qty));
            }

            if (stockIssues.Count > 0)
            {
                return BadRequest(new
                {
                    message = "Some products are out of stock. Please update your cart.",
                    issues = stockIssues
                });
            }

            foreach (var (product, qty) in productsToUpdate)
            {
                product.Stock = Math.Max(0, product.Stock - qty);
                await EnsureLowStockAlert(product);
            }

            order.Id = orders.Count == 0 ? 1 : orders.Max(item => item.Id) + 1;
            order.Status = "Pending";
            order.OrderNumber = string.IsNullOrWhiteSpace(order.OrderNumber)
                ? GenerateOrderNumber(order.Id)
                : order.OrderNumber.Trim().ToUpperInvariant();
            order.TrackingNumber = string.IsNullOrWhiteSpace(order.TrackingNumber)
                ? $"TRK{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()[^6..]}"
                : order.TrackingNumber.Trim();
            order.CreatedAt = order.CreatedAt == default ? DateTime.UtcNow : order.CreatedAt;
            order.UpdatedAt = DateTime.UtcNow;
            order.StatusHistory = new List<OrderStatusHistoryEntry>
            {
                new OrderStatusHistoryEntry
                {
                    Status = order.Status,
                    Notes = "Order placed",
                    UpdatedBy = "system",
                    UpdatedAt = DateTime.UtcNow
                }
            };

            orders.Add(order);
            await NotifyAdminOnNewOrder(order);
            return Ok(order);
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult GetAllOrders()
        {
            return Ok(orders.OrderByDescending(order => order.CreatedAt).ToList());
        }

        [HttpGet("me")]
        [AllowAnonymous]
        public IActionResult GetMyOrders([FromQuery] int? userId = null)
        {
            IEnumerable<Order> query = orders;
            if (userId.HasValue && userId.Value > 0)
            {
                query = query.Where(order => order.UserId == userId.Value);
            }

            var role = User.FindFirstValue(ClaimTypes.Role) ?? "";
            var requesterIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var requesterId = int.TryParse(requesterIdClaim, out var parsedId) ? parsedId : 0;

            if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) && requesterId > 0 && !userId.HasValue)
            {
                query = query.Where(order => order.UserId == requesterId);
            }

            return Ok(query.OrderByDescending(order => order.CreatedAt).ToList());
        }

        [HttpPut("{id:int}/status")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult UpdateStatus(int id, [FromBody] UpdateOrderStatusRequest request)
        {
            var order = orders.FirstOrDefault(item => item.Id == id);
            if (order == null)
            {
                return NotFound("Order not found.");
            }

            var nextStatus = (request.Status ?? "").Trim();
            if (!AllowedStatuses.Contains(nextStatus))
            {
                return BadRequest(new { message = "Invalid status." });
            }

            var currentStatus = order.Status ?? "Pending";
            if (!AllowedTransitions.TryGetValue(currentStatus, out var allowedNext))
            {
                allowedNext = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            if (!string.Equals(currentStatus, nextStatus, StringComparison.OrdinalIgnoreCase) && !allowedNext.Contains(nextStatus))
            {
                return BadRequest(new { message = $"Status cannot move from {currentStatus} to {nextStatus}." });
            }

            var actorEmail = User.FindFirstValue(ClaimTypes.Email) ?? "admin";
            order.Status = nextStatus;
            order.UpdatedAt = DateTime.UtcNow;
            order.StatusHistory ??= new List<OrderStatusHistoryEntry>();
            order.StatusHistory.Add(new OrderStatusHistoryEntry
            {
                Status = nextStatus,
                Notes = (request.Notes ?? "").Trim(),
                UpdatedBy = actorEmail,
                UpdatedAt = DateTime.UtcNow
            });

            AuditLogStore.Add(User, "UpdateStatus", "Order", order.Id.ToString(), $"Changed status from {currentStatus} to {nextStatus}.");
            return Ok(order);
        }

        [HttpPost("track")]
        [AllowAnonymous]
        public IActionResult TrackOrder([FromBody] TrackOrderRequest request)
        {
            var rawOrderId = (request.OrderId ?? "").Trim();

            var contact = (request.Contact ?? "").Trim();
            var normalizedContact = contact.ToLowerInvariant();
            var normalizedPhone = NormalizePhone(contact);

            var rawOrderIdUpper = rawOrderId.ToUpperInvariant();
            var order = orders.FirstOrDefault(o =>
                (
                    string.Equals((o.OrderNumber ?? "").Trim().ToUpperInvariant(), rawOrderIdUpper, StringComparison.Ordinal) ||
                    string.Equals(o.Id.ToString(), rawOrderId, StringComparison.Ordinal)
                ) &&
                (
                    string.IsNullOrWhiteSpace(contact) ||
                    string.Equals((o.Email ?? "").Trim().ToLowerInvariant(), normalizedContact, StringComparison.Ordinal) ||
                    string.Equals(NormalizePhone(o.Phone), normalizedPhone, StringComparison.Ordinal)
                ));

            if (order == null)
            {
                return NotFound("Order not found");
            }

            return Ok(order);
        }

        private int GetLowStockThreshold()
        {
            var configured = _configuration.GetValue<int?>("Inventory:LowStockThreshold") ?? 5;
            return Math.Max(1, configured);
        }

        private async Task EnsureLowStockAlert(Product product)
        {
            var threshold = GetLowStockThreshold();
            if (product.Stock <= threshold)
            {
                if (InventoryAlertStore.IsLowStockAlertSent(product.Id))
                {
                    return;
                }

                var sendEnabled = _configuration.GetValue<bool?>("Inventory:SendLowStockEmails") ?? true;
                var recipient = _configuration["Inventory:AdminAlertEmail"] ?? "patilpiyush1619@gmail.com";
                if (sendEnabled)
                {
                    var subject = $"Low stock alert: {product.Name}";
                    var body =
                        $"Product: {product.Name}\n" +
                        $"Stock left: {product.Stock}\n" +
                        $"Threshold: {threshold}\n" +
                        $"Admin panel: http://127.0.0.1:5174/products/{product.Id}/edit";
                    await TrySendEmail(recipient, subject, body);
                }

                InventoryAlertStore.MarkLowStockAlertSent(product.Id);
                _logger.LogWarning("Low stock alert triggered for product {ProductId} ({ProductName}).", product.Id, product.Name);
                return;
            }

            InventoryAlertStore.ClearLowStockAlert(product.Id);
        }

        private async Task<bool> TrySendEmail(string recipientEmail, string subject, string body)
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
                await Task.CompletedTask;
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

        private static string NormalizePhone(string value)
        {
            return new string((value ?? "").Where(char.IsDigit).ToArray());
        }

        private static bool TryResolveProductId(string rawProductId, out int productId)
        {
            productId = 0;
            var input = (rawProductId ?? "").Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            if (int.TryParse(input, out productId))
            {
                return true;
            }

            var fromVariantKey = input.Split("__", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(fromVariantKey) && int.TryParse(fromVariantKey, out productId))
            {
                return true;
            }

            var onlyDigits = new string(input.Where(char.IsDigit).ToArray());
            return !string.IsNullOrWhiteSpace(onlyDigits) && int.TryParse(onlyDigits, out productId);
        }

        private static string GenerateOrderNumber(int id)
        {
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var sequencePart = id.ToString("D4");
            return $"YF-{datePart}-{sequencePart}";
        }

        private async Task NotifyAdminOnNewOrder(Order order)
        {
            var receiverEmail =
                _configuration["OrderAlerts:ReceiverEmail"] ??
                _configuration["SupportAlerts:ReceiverEmail"] ??
                "patilpiyush1619@gmail.com";

            var subject = $"New Order Received: {order.OrderNumber}";
            var itemsSummary = string.Join(
                ", ",
                order.Items.Select(item =>
                {
                    var qty = Math.Max(1, item.Qty);
                    return $"{item.Title} x{qty}";
                })
            );

            var body =
                $"A new order has been placed.\n\n" +
                $"Order Number: {order.OrderNumber}\n" +
                $"Internal ID: {order.Id}\n" +
                $"Customer: {order.Name}\n" +
                $"Email: {order.Email}\n" +
                $"Phone: {order.Phone}\n" +
                $"City: {order.City}\n" +
                $"Total: Rs {order.Total}\n" +
                $"Payment: {order.Payment}\n" +
                $"Items: {itemsSummary}\n" +
                $"Placed At (UTC): {order.CreatedAt:u}\n\n" +
                $"Admin Panel: http://127.0.0.1:5174/orders";

            var sent = await TrySendEmail(receiverEmail, subject, body);
            if (sent)
            {
                _logger.LogInformation("New order alert sent to admin for order {OrderNumber}.", order.OrderNumber);
            }
            else
            {
                _logger.LogWarning("New order alert could not be sent for order {OrderNumber}.", order.OrderNumber);
            }
        }
    }
}
