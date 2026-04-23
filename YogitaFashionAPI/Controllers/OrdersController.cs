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
        private readonly INotificationService _notificationService;
        private readonly IAuditLogService _auditLogService;
        private readonly AppDbContext _db;

        public OrdersController(
            IConfiguration configuration,
            ILogger<OrdersController> logger,
            INotificationService notificationService,
            IAuditLogService auditLogService,
            AppDbContext db)
        {
            _configuration = configuration;
            _logger = logger;
            _notificationService = notificationService;
            _auditLogService = auditLogService;
            _db = db;
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> CreateOrder([FromBody] Order input)
        {
            if (input == null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "OrderCreate", "Order", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "missing_payload"
                });
                return BadRequest("Order payload is required.");
            }

            if (string.IsNullOrWhiteSpace(input.Name) ||
                string.IsNullOrWhiteSpace(input.Address) ||
                string.IsNullOrWhiteSpace(input.City) ||
                string.IsNullOrWhiteSpace(input.Pincode))
            {
                await _auditLogService.WriteAsync(User, HttpContext, "OrderCreate", "Order", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "missing_delivery_fields"
                });
                return BadRequest("Name, address, city and pincode are required.");
            }

            if (string.IsNullOrWhiteSpace(input.Phone) && string.IsNullOrWhiteSpace(input.Email))
            {
                await _auditLogService.WriteAsync(User, HttpContext, "OrderCreate", "Order", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "missing_contact"
                });
                return BadRequest("Phone or email is required.");
            }

            var normalizedItems = (input.Items ?? new List<OrderItem>())
                .Select(NormalizeItem)
                .ToList();

            if (normalizedItems.Count == 0)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "OrderCreate", "Order", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "empty_items"
                });
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
                await _auditLogService.WriteAsync(User, HttpContext, "OrderCreate", "Order", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "invalid_product_ids",
                    ["issueCount"] = stockIssues.Count
                });
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
                await _auditLogService.WriteAsync(User, HttpContext, "OrderCreate", "Order", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "stock_conflict",
                    ["issueCount"] = stockIssues.Count
                });
                return BadRequest(new
                {
                    message = "Some products are out of stock. Please update your cart.",
                    issues = stockIssues
                });
            }

            var serverItems = requestedLines
                .Select(line => BuildOrderItem(line.item, productMap[line.productId], line.qty))
                .ToList();

            foreach (var pair in requestedQtyByProduct)
            {
                var product = productMap[pair.Key];
                product.Stock = Math.Max(0, product.Stock - pair.Value);
            }

            var subtotal = Math.Round(serverItems.Sum(item => Math.Max(0, item.Price) * Math.Max(1, item.Qty)), 2);
            var now = DateTime.UtcNow;
            var requesterId = GetCurrentUserId();
            var effectiveUserId = requesterId;

            var couponCode = (input.CouponCode ?? "").Trim().ToUpperInvariant();
            var discountAmount = 0m;
            int? couponId = null;
            if (!string.IsNullOrWhiteSpace(couponCode))
            {
                var couponResult = await ValidateAndConsumeCouponAsync(couponCode, subtotal, effectiveUserId.GetValueOrDefault(), now);
                if (!couponResult.Valid)
                {
                    await transaction.RollbackAsync();
                    await _auditLogService.WriteAsync(User, HttpContext, "OrderCreate", "Order", status: "Failure", metadata: new Dictionary<string, object?>
                    {
                        ["reason"] = "coupon_validation_failed",
                        ["couponCode"] = couponCode
                    }, userIdOverride: effectiveUserId.GetValueOrDefault() > 0 ? effectiveUserId : null);
                    return BadRequest(new { message = couponResult.Message });
                }

                couponCode = couponResult.Code;
                discountAmount = couponResult.DiscountAmount;
                couponId = couponResult.CouponId;
            }

            var total = Math.Max(0, Math.Round(subtotal - discountAmount, 2));
            var order = new Order
            {
                UserId = effectiveUserId,
                CouponId = couponId,
                Name = (input.Name ?? "").Trim(),
                Phone = (input.Phone ?? "").Trim(),
                Email = ResolveOrderEmail(input.Email),
                Address = (input.Address ?? "").Trim(),
                City = (input.City ?? "").Trim(),
                Pincode = (input.Pincode ?? "").Trim(),
                Payment = NormalizePayment(input.Payment),
                Total = total,
                CouponCode = couponCode,
                DiscountAmount = discountAmount,
                Items = serverItems,
                Status = "Pending",
                OrderNumber = string.Empty,
                TrackingNumber = GenerateTrackingNumber(),
                CreatedAt = now,
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
                },
                LineItems = serverItems
                    .Select(BuildOrderLineItem)
                    .ToList()
            };

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            order.OrderNumber = GenerateOrderNumber(order.Id);
            await _db.SaveChangesAsync();

            await transaction.CommitAsync();

            foreach (var product in products)
            {
                await EnsureLowStockAlert(product);
            }

            await _auditLogService.WriteAsync(User, HttpContext, "OrderCreate", "Order", order.Id.ToString(), "Success", new Dictionary<string, object?>
            {
                ["orderNumber"] = order.OrderNumber,
                ["payment"] = order.Payment,
                ["total"] = order.Total,
                ["itemCount"] = order.Items.Count,
                ["couponCode"] = order.CouponCode
            }, order.UserId.GetValueOrDefault() > 0 ? order.UserId : null);
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
        [Authorize]
        public async Task<IActionResult> GetMyOrders([FromQuery] int? userId = null)
        {
            var requesterId = GetCurrentUserId();
            var isAdmin = IsAdmin();
            if (!isAdmin && !requesterId.HasValue)
            {
                return Unauthorized();
            }

            IQueryable<Order> query = _db.Orders;

            if (isAdmin)
            {
                if (userId.HasValue && userId.Value > 0)
                {
                    query = query.Where(order => order.UserId == userId.Value);
                }
                else if (requesterId.HasValue && requesterId.Value > 0)
                {
                    query = query.Where(order => order.UserId == requesterId.Value);
                }
                else
                {
                    return Ok(new List<Order>());
                }
            }
            else
            {
                query = query.Where(order => order.UserId == requesterId!.Value);
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
            if (request == null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "OrderStatusUpdate", "Order", id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "missing_payload"
                });
                return BadRequest(new { message = "Status payload is required." });
            }

            var order = await _db.Orders.FirstOrDefaultAsync(item => item.Id == id);
            if (order == null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "OrderStatusUpdate", "Order", id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "not_found"
                });
                return NotFound("Order not found.");
            }

            var nextStatus = (request.Status ?? "").Trim();
            if (!AllowedStatuses.Contains(nextStatus))
            {
                await _auditLogService.WriteAsync(User, HttpContext, "OrderStatusUpdate", "Order", id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "invalid_status",
                    ["status"] = nextStatus
                }, order.UserId.GetValueOrDefault() > 0 ? order.UserId : null);
                return BadRequest(new { message = "Invalid status." });
            }

            var currentStatus = order.Status ?? "Pending";
            if (!AllowedTransitions.TryGetValue(currentStatus, out var allowedNext))
            {
                allowedNext = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            if (!string.Equals(currentStatus, nextStatus, StringComparison.OrdinalIgnoreCase) && !allowedNext.Contains(nextStatus))
            {
                await _auditLogService.WriteAsync(User, HttpContext, "OrderStatusUpdate", "Order", id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "invalid_transition",
                    ["fromStatus"] = currentStatus,
                    ["toStatus"] = nextStatus
                }, order.UserId.GetValueOrDefault() > 0 ? order.UserId : null);
                return BadRequest(new { message = $"Status cannot move from {currentStatus} to {nextStatus}." });
            }

            var actorEmail = User.FindFirstValue(ClaimTypes.Email) ?? "admin";
            var now = DateTime.UtcNow;
            order.Status = nextStatus;
            order.UpdatedAt = now;
            order.StatusHistory ??= new List<OrderStatusHistoryEntry>();
            order.StatusHistory.Add(new OrderStatusHistoryEntry
            {
                Status = nextStatus,
                Notes = (request.Notes ?? "").Trim(),
                UpdatedBy = actorEmail,
                UpdatedAt = now
            });

            await _db.SaveChangesAsync();

            await _auditLogService.WriteAsync(User, HttpContext, "OrderStatusUpdate", "Order", order.Id.ToString(), "Success", new Dictionary<string, object?>
            {
                ["fromStatus"] = currentStatus,
                ["toStatus"] = nextStatus,
                ["notes"] = request.Notes
            }, order.UserId.GetValueOrDefault() > 0 ? order.UserId : null);
            return Ok(order);
        }

        [HttpPost("track")]
        [AllowAnonymous]
        public async Task<IActionResult> TrackOrder([FromBody] TrackOrderRequest request)
        {
            if (request == null)
            {
                return BadRequest("Tracking payload is required.");
            }

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

            if (IsAdmin())
            {
                return Ok(order);
            }

            var requesterId = GetCurrentUserId();
            if (requesterId.HasValue && order.UserId.GetValueOrDefault() > 0 && requesterId.Value == order.UserId)
            {
                return Ok(order);
            }

            if (requesterId.HasValue && order.UserId.GetValueOrDefault() > 0 && requesterId.Value != order.UserId)
            {
                return NotFound("Order not found");
            }

            var contact = (request.Contact ?? "").Trim();
            if (string.IsNullOrWhiteSpace(contact))
            {
                return BadRequest("Contact is required.");
            }

            var normalizedContact = contact.ToLowerInvariant();
            var normalizedPhone = NormalizePhone(contact);
            var emailMatches = string.Equals((order.Email ?? "").Trim().ToLowerInvariant(), normalizedContact, StringComparison.Ordinal);
            var phoneMatches = string.Equals(NormalizePhone(order.Phone), normalizedPhone, StringComparison.Ordinal);

            if (!emailMatches && !phoneMatches)
            {
                return NotFound("Order not found");
            }

            return Ok(order);
        }

        private static OrderItem NormalizeItem(OrderItem input)
        {
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
                Size = (input.Size ?? "").Trim(),
                Color = (input.Color ?? "").Trim(),
                Qty = qty
            };
        }

        private static OrderItem BuildOrderItem(OrderItem clientItem, Product product, int qty)
        {
            var resolvedSize = !string.IsNullOrWhiteSpace(clientItem.Size)
                ? clientItem.Size.Trim()
                : product.Sizes.FirstOrDefault() ?? "";

            var resolvedColor = !string.IsNullOrWhiteSpace(clientItem.Color)
                ? clientItem.Color.Trim()
                : product.Colors.FirstOrDefault() ?? "";

            var image = (product.ImageUrl ?? "").Trim();
            var price = Math.Max(0, product.Price);
            var mrp = product.OriginalPrice > 0 ? product.OriginalPrice : (product.Mrp > 0 ? product.Mrp : price);

            return new OrderItem
            {
                Key = clientItem.Key,
                ProductId = product.Id.ToString(),
                Title = string.IsNullOrWhiteSpace(product.Name) ? (clientItem.Title ?? "").Trim() : product.Name.Trim(),
                Image = image,
                Price = Math.Round(price, 2),
                Mrp = Math.Round(Math.Max(price, mrp), 2),
                Category = (product.Category ?? "").Trim(),
                Size = resolvedSize,
                Color = resolvedColor,
                Qty = Math.Max(1, qty)
            };
        }
    
        private static OrderLineItem BuildOrderLineItem(OrderItem item)
        {
            TryResolveProductId(item.ProductId, out var productId);
            return new OrderLineItem
            {
                ProductId = productId,
                ItemKey = (item.Key ?? "").Trim(),
                Title = (item.Title ?? "").Trim(),
                Image = (item.Image ?? "").Trim(),
                Price = Math.Round(Math.Max(0, item.Price), 2),
                Mrp = Math.Round(Math.Max(0, item.Mrp), 2),
                Category = (item.Category ?? "").Trim(),
                Size = (item.Size ?? "").Trim(),
                Color = (item.Color ?? "").Trim(),
                Quantity = Math.Max(1, item.Qty),
                CreatedAt = DateTime.UtcNow
            };
        }

        private async Task<(bool Valid, string Message, string Code, decimal DiscountAmount, int? CouponId)> ValidateAndConsumeCouponAsync(
            string rawCode,
            decimal subtotal,
            int userId,
            DateTime nowUtc)
        {
            var code = (rawCode ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(code))
            {
                await _auditLogService.WriteAsync(User, HttpContext, "CouponUsage", "Coupon", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "missing_code"
                }, userIdOverride: userId > 0 ? userId : null);
                return (false, "Coupon code is required.", string.Empty, 0, null);
            }

            var coupon = await _db.Coupons.FirstOrDefaultAsync(item =>
                (item.Code ?? "").ToUpper() == code);
            if (coupon == null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "CouponUsage", "Coupon", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "invalid_coupon",
                    ["code"] = code
                }, userIdOverride: userId > 0 ? userId : null);
                return (false, "Invalid coupon.", string.Empty, 0, null);
            }

            if (!coupon.IsActive)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "CouponUsage", "Coupon", coupon.Id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "inactive_coupon",
                    ["code"] = code
                }, userId > 0 ? userId : null);
                return (false, "Coupon is inactive.", string.Empty, 0, null);
            }

            if (coupon.StartAt.HasValue && ToUtc(coupon.StartAt.Value) > nowUtc)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "CouponUsage", "Coupon", coupon.Id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "coupon_not_started",
                    ["code"] = code
                }, userId > 0 ? userId : null);
                return (false, "Coupon is not active yet.", string.Empty, 0, null);
            }

            if (coupon.EndAt.HasValue && ToUtc(coupon.EndAt.Value) < nowUtc)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "CouponUsage", "Coupon", coupon.Id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "coupon_expired",
                    ["code"] = code
                }, userId > 0 ? userId : null);
                return (false, "Coupon has expired.", string.Empty, 0, null);
            }

            if (coupon.MaxUses > 0 && coupon.UsedCount >= coupon.MaxUses)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "CouponUsage", "Coupon", coupon.Id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "coupon_max_usage_reached",
                    ["code"] = code
                }, userId > 0 ? userId : null);
                return (false, "Coupon usage limit reached.", string.Empty, 0, null);
            }

            if (subtotal < coupon.MinOrderAmount)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "CouponUsage", "Coupon", coupon.Id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "minimum_amount_not_met",
                    ["code"] = code,
                    ["subtotal"] = subtotal
                }, userId > 0 ? userId : null);
                return (false, $"Minimum order value Rs {coupon.MinOrderAmount} required.", string.Empty, 0, null);
            }

            CouponUsageRecord? usage = null;
            if (userId > 0 && coupon.MaxUsesPerUser > 0)
            {
                usage = await _db.CouponUsageRecords.FirstOrDefaultAsync(item =>
                    item.CouponId == coupon.Id && item.UserId == userId);

                if (usage != null && usage.Count >= coupon.MaxUsesPerUser)
                {
                    await _auditLogService.WriteAsync(User, HttpContext, "CouponUsage", "Coupon", coupon.Id.ToString(), "Failure", new Dictionary<string, object?>
                    {
                        ["reason"] = "user_usage_limit_reached",
                        ["code"] = code
                    }, userId);
                    return (false, "You have already used this coupon the maximum number of times.", string.Empty, 0, null);
                }
            }

            var discountAmount = CalculateDiscount(coupon, subtotal);
            if (discountAmount <= 0)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "CouponUsage", "Coupon", coupon.Id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "non_positive_discount",
                    ["code"] = code,
                    ["subtotal"] = subtotal
                }, userId > 0 ? userId : null);
                return (false, "Coupon is not applicable to this order.", string.Empty, 0, null);
            }

            coupon.UsedCount += 1;
            coupon.UpdatedAt = nowUtc;

            if (userId > 0)
            {
                if (usage == null)
                {
                    _db.CouponUsageRecords.Add(new CouponUsageRecord
                    {
                        CouponId = coupon.Id,
                        UserId = userId,
                        Count = 1
                    });
                }
                else
                {
                    usage.Count += 1;
                }
            }

            await _auditLogService.WriteAsync(User, HttpContext, "CouponUsage", "Coupon", coupon.Id.ToString(), "Success", new Dictionary<string, object?>
            {
                ["code"] = code,
                ["discountAmount"] = discountAmount,
                ["subtotal"] = subtotal
            }, userId > 0 ? userId : null);
            return (true, "Coupon applied successfully.", code, discountAmount, coupon.Id);
        }

        private static decimal CalculateDiscount(Coupon coupon, decimal subtotal)
        {
            if (subtotal <= 0 || coupon.Value <= 0)
            {
                return 0;
            }

            if (string.Equals(coupon.Type, "fixed", StringComparison.OrdinalIgnoreCase))
            {
                return Math.Min(subtotal, coupon.Value);
            }

            var percentAmount = subtotal * (coupon.Value / 100m);
            return Math.Min(subtotal, Math.Max(0, Math.Round(percentAmount, 2)));
        }

        private static DateTime ToUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc)
            {
                return value;
            }

            if (value.Kind == DateTimeKind.Unspecified)
            {
                return DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime();
            }

            return value.ToUniversalTime();
        }

        private static string NormalizePayment(string value)
        {
            var normalized = (value ?? "").Trim().ToUpperInvariant();
            return normalized switch
            {
                "ONLINE" => "ONLINE",
                _ => "COD"
            };
        }

        private string ResolveOrderEmail(string requestEmail)
        {
            var normalizedEmail = (requestEmail ?? "").Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(normalizedEmail))
            {
                return normalizedEmail;
            }

            return (User.FindFirstValue(ClaimTypes.Email) ?? "").Trim().ToLowerInvariant();
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
                        $"Admin panel: {_notificationService.GetAdminPanelBaseUrl()}/products/{product.Id}/edit";
                    await _notificationService.SendEmailAsync(recipient, subject, body);
                }

                InventoryAlertStore.MarkLowStockAlertSent(product.Id);
                _logger.LogWarning("Low stock alert triggered for product {ProductId} ({ProductName}).", product.Id, product.Name);
                return;
            }

            InventoryAlertStore.ClearLowStockAlert(product.Id);
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
                $"Admin Panel: {_notificationService.GetAdminPanelBaseUrl()}/orders";

            var sent = await _notificationService.SendEmailAsync(receiverEmail, subject, body);
            if (sent)
            {
                _logger.LogInformation("New order alert sent to admin for order {OrderNumber}.", order.OrderNumber);
            }
            else
            {
                _logger.LogWarning("New order alert could not be sent for order {OrderNumber}.", order.OrderNumber);
            }
        }

        private int? GetCurrentUserId()
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(idClaim, out var parsedId) ? parsedId : null;
        }

        private bool IsAdmin()
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? "";
            return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
        }
    }
}
