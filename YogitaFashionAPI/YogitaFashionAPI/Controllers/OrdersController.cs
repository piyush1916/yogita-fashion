using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YogitaFashionAPI.Data;
using YogitaFashionAPI.Models;
using YogitaFashionAPI.Services;

namespace YogitaFashionAPI.Controllers
{
    [Route("orders")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private const string DefaultFrontendBaseUrl = "http://127.0.0.1:5173";
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
        private readonly AppDbContext _db;

        public OrdersController(
            IConfiguration configuration,
            ILogger<OrdersController> logger,
            AppDbContext db)
        {
            _configuration = configuration;
            _logger = logger;
            _db = db;
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> CreateOrder([FromBody] Order input)
        {
            var normalizedItems = (input.Items ?? new List<OrderItem>())
                .Select(NormalizeItem)
                .ToList();

            if (normalizedItems.Count == 0)
            {
                return BadRequest("Order items are required.");
            }

            var stockIssues = new List<object>();
            var requestedLines = new List<(OrderItem item, int productId, int qty)>();
            foreach (var item in normalizedItems)
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

                var qty = Math.Max(1, item.Qty);
                requestedLines.Add((item, productId, qty));
            }

            if (stockIssues.Count > 0)
            {
                return BadRequest(new
                {
                    message = "Some products are out of stock. Please update your cart.",
                    issues = stockIssues
                });
            }

            var requestedQtyByProduct = requestedLines
                .GroupBy(line => line.productId)
                .ToDictionary(group => group.Key, group => group.Sum(line => line.qty));

            await using var transaction = await _db.Database.BeginTransactionAsync();

            var productIds = requestedQtyByProduct.Keys.ToList();
            var products = await _db.Products
                .Where(product => productIds.Contains(product.Id))
                .ToListAsync();
            var productMap = products.ToDictionary(product => product.Id);

            foreach (var pair in requestedQtyByProduct)
            {
                var productId = pair.Key;
                var requestedQty = pair.Value;

                if (!productMap.TryGetValue(productId, out var product))
                {
                    var line = requestedLines.First(item => item.productId == productId).item;
                    stockIssues.Add(new
                    {
                        productId,
                        line.Title,
                        message = "Product not found."
                    });
                    continue;
                }

                if (product.Stock < requestedQty)
                {
                    stockIssues.Add(new
                    {
                        productId,
                        product.Name,
                        requested = requestedQty,
                        available = product.Stock,
                        message = "Out of stock."
                    });
                }
            }

            if (stockIssues.Count > 0)
            {
                await transaction.RollbackAsync();
                return BadRequest(new
                {
                    message = "Some products are out of stock. Please update your cart.",
                    issues = stockIssues
                });
            }

            foreach (var pair in requestedQtyByProduct)
            {
                var product = productMap[pair.Key];
                product.Stock = Math.Max(0, product.Stock - pair.Value);
            }

            var now = DateTime.UtcNow;
            var order = new Order
            {
                UserId = input.UserId,
                Name = (input.Name ?? "").Trim(),
                Phone = (input.Phone ?? "").Trim(),
                Email = (input.Email ?? "").Trim(),
                Address = (input.Address ?? "").Trim(),
                City = (input.City ?? "").Trim(),
                Pincode = (input.Pincode ?? "").Trim(),
                Payment = string.IsNullOrWhiteSpace(input.Payment) ? "COD" : input.Payment.Trim(),
                Total = NormalizeTotal(input.Total, normalizedItems),
                Items = normalizedItems,
                Status = "Pending",
                OrderNumber = string.IsNullOrWhiteSpace(input.OrderNumber)
                    ? string.Empty
                    : input.OrderNumber.Trim().ToUpperInvariant(),
                TrackingNumber = string.IsNullOrWhiteSpace(input.TrackingNumber)
                    ? GenerateTrackingNumber()
                    : input.TrackingNumber.Trim(),
                CreatedAt = input.CreatedAt == default ? now : input.CreatedAt,
                UpdatedAt = now,
                StatusHistory = new List<OrderStatusHistoryEntry>
                {
                    new OrderStatusHistoryEntry
                    {
                        Status = "Pending",
                        Notes = "Order placed",
                        UpdatedBy = "system",
                        UpdatedAt = now
                    }
                }
            };

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            if (string.IsNullOrWhiteSpace(order.OrderNumber))
            {
                order.OrderNumber = GenerateOrderNumber(order.Id);
                await _db.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            foreach (var product in products)
            {
                await EnsureLowStockAlert(product);
            }

            await NotifyAdminOnNewOrder(order);
            return Ok(order);
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetAllOrders()
        {
            var items = await _db.Orders
                .OrderByDescending(order => order.CreatedAt)
                .ToListAsync();
            return Ok(items);
        }

        [HttpGet("me")]
        [AllowAnonymous]
        public async Task<IActionResult> GetMyOrders([FromQuery] int? userId = null)
        {
            IQueryable<Order> query = _db.Orders;
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

            var items = await query
                .OrderByDescending(order => order.CreatedAt)
                .ToListAsync();
            return Ok(items);
        }

        [HttpPut("{id:int}/status")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateOrderStatusRequest request)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(item => item.Id == id);
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

            await _db.SaveChangesAsync();

            AuditLogStore.Add(User, "UpdateStatus", "Order", order.Id.ToString(), $"Changed status from {currentStatus} to {nextStatus}.");
            return Ok(order);
        }

        [HttpPost("track")]
        [AllowAnonymous]
        public async Task<IActionResult> TrackOrder([FromBody] TrackOrderRequest request)
        {
            var rawOrderId = (request.OrderId ?? "").Trim();
            var rawOrderIdUpper = rawOrderId.ToUpperInvariant();

            Order? order = null;
            if (int.TryParse(rawOrderId, out var numericOrderId))
            {
                order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(item => item.Id == numericOrderId);
            }

            if (order == null && !string.IsNullOrWhiteSpace(rawOrderId))
            {
                order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(item =>
                    (item.OrderNumber ?? string.Empty).ToUpper() == rawOrderIdUpper);
            }

            if (order == null)
            {
                return NotFound("Order not found");
            }

            var contact = (request.Contact ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(contact))
            {
                var normalizedContact = contact.ToLowerInvariant();
                var normalizedPhone = NormalizePhone(contact);
                var emailMatches = string.Equals((order.Email ?? "").Trim().ToLowerInvariant(), normalizedContact, StringComparison.Ordinal);
                var phoneMatches = string.Equals(NormalizePhone(order.Phone), normalizedPhone, StringComparison.Ordinal);

                if (!emailMatches && !phoneMatches)
                {
                    return NotFound("Order not found");
                }
            }

            return Ok(order);
        }

        private static OrderItem NormalizeItem(OrderItem input)
        {
            var price = Math.Max(0, input.Price);
            var mrp = input.Mrp > 0 ? input.Mrp : price;
            var qty = Math.Max(1, input.Qty);
            var productId = (input.ProductId ?? "").Trim();
            var key = string.IsNullOrWhiteSpace(input.Key)
                ? $"{(string.IsNullOrWhiteSpace(productId) ? "item" : productId)}_{Guid.NewGuid():N}"
                : input.Key.Trim();

            return new OrderItem
            {
                Key = key,
                ProductId = productId,
                Title = (input.Title ?? "").Trim(),
                Image = (input.Image ?? "").Trim(),
                Price = price,
                Mrp = Math.Max(price, mrp),
                Category = (input.Category ?? "").Trim(),
                Size = (input.Size ?? "").Trim(),
                Color = (input.Color ?? "").Trim(),
                Qty = qty
            };
        }

        private static decimal NormalizeTotal(decimal total, IEnumerable<OrderItem> items)
        {
            if (total > 0)
            {
                return Math.Round(total, 2);
            }

            var computed = items.Sum(item => Math.Max(0, item.Price) * Math.Max(1, item.Qty));
            return Math.Round(Math.Max(0, computed), 2);
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
                        $"Admin panel: {GetAdminPanelBaseUrl()}/products/{product.Id}/edit";
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

        private static string GenerateTrackingNumber()
        {
            var raw = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            var suffix = raw.Length >= 6 ? raw[^6..] : raw.PadLeft(6, '0');
            return $"TRK{suffix}";
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
                $"Admin Panel: {GetAdminPanelBaseUrl()}/orders";

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

        private string GetAdminPanelBaseUrl()
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
