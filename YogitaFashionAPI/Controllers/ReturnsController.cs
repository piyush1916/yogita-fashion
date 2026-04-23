using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YogitaFashionAPI.Data;
using YogitaFashionAPI.Models;
using YogitaFashionAPI.Services;

namespace YogitaFashionAPI.Controllers
{
    [ApiController]
    [Route("returns")]
    public class ReturnsController : ControllerBase
    {
        private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Pending",
            "Approved",
            "Pickup Started",
            "Pickup Completed",
            "Completed",
            "Rejected",
            "Refunded"
        };

        private readonly AppDbContext _db;
        private readonly IAuditLogService _auditLogService;

        public ReturnsController(AppDbContext db, IAuditLogService auditLogService)
        {
            _db = db;
            _auditLogService = auditLogService;
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateReturn([FromBody] CreateReturnRequest input)
        {
            if (input == null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "ReturnRequestCreate", "ReturnRequest", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "missing_payload"
                });
                return BadRequest(new { message = "Return request payload is required." });
            }

            var requesterId = GetCurrentUserId();
            var role = User.FindFirstValue(ClaimTypes.Role) ?? "";
            if (!requesterId.HasValue && !string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                await _auditLogService.WriteAsync(User, HttpContext, "ReturnRequestCreate", "ReturnRequest", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "unauthorized",
                    ["orderId"] = input.OrderId
                });
                return Unauthorized();
            }

            var order = await _db.Orders.FirstOrDefaultAsync(item => item.Id == input.OrderId);
            if (order == null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "ReturnRequestCreate", "ReturnRequest", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "order_not_found",
                    ["orderId"] = input.OrderId
                });
                return NotFound(new { message = "Order not found." });
            }

            var orderUserId = order.UserId.GetValueOrDefault();
            if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) &&
                (!requesterId.HasValue || orderUserId <= 0 || requesterId.Value != orderUserId))
            {
                await _auditLogService.WriteAsync(User, HttpContext, "ReturnRequestCreate", "ReturnRequest", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "forbidden",
                    ["orderId"] = input.OrderId
                });
                return Forbid();
            }

            if (!string.Equals(order.Status, "Delivered", StringComparison.OrdinalIgnoreCase))
            {
                await _auditLogService.WriteAsync(User, HttpContext, "ReturnRequestCreate", "ReturnRequest", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "order_not_delivered",
                    ["orderId"] = input.OrderId
                });
                return BadRequest(new { message = "Return can only be requested after delivery." });
            }

            if (orderUserId <= 0)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "ReturnRequestCreate", "ReturnRequest", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "order_has_no_registered_user",
                    ["orderId"] = input.OrderId
                });
                return BadRequest(new { message = "Return requires an order linked to a registered user." });
            }

            var productId = (input.ItemProductId ?? "").Trim();
            var productIdUpper = productId.ToUpperInvariant();
            var orderItem = order.Items.FirstOrDefault(item => string.Equals(item.ProductId, productId, StringComparison.OrdinalIgnoreCase));
            if (orderItem == null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "ReturnRequestCreate", "ReturnRequest", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "product_not_in_order",
                    ["orderId"] = input.OrderId,
                    ["itemProductId"] = productId
                });
                return BadRequest(new { message = "Product not found in this order." });
            }

            var duplicate = await _db.ReturnRequests.FirstOrDefaultAsync(item =>
                item.OrderId == order.Id &&
                (item.ItemProductId ?? "").ToUpper() == productIdUpper &&
                (item.Status ?? "").ToUpper() != "REJECTED");
            if (duplicate != null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "ReturnRequestCreate", "ReturnRequest", duplicate.Id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "duplicate_request",
                    ["orderId"] = input.OrderId,
                    ["itemProductId"] = productId
                }, orderUserId > 0 ? orderUserId : null);
                return Conflict(new { message = "Return request already exists for this item.", duplicate.Id });
            }

            var refundAmount = Math.Round(orderItem.Price * Math.Max(1, orderItem.Qty), 2);
            var request = new ReturnRequest
            {
                OrderId = order.Id,
                UserId = orderUserId,
                ProductId = TryResolveProductId(orderItem.ProductId, out var parsedProductId) ? parsedProductId : null,
                ItemProductId = orderItem.ProductId,
                ItemTitle = orderItem.Title,
                Quantity = Math.Max(1, orderItem.Qty),
                RefundAmount = refundAmount,
                Reason = (input.Reason ?? "").Trim(),
                CustomerRemark = (input.CustomerRemark ?? "").Trim(),
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.ReturnRequests.Add(request);
            await _db.SaveChangesAsync();
            await _auditLogService.WriteAsync(User, HttpContext, "ReturnRequestCreate", "ReturnRequest", request.Id.ToString(), "Success", new Dictionary<string, object?>
            {
                ["orderId"] = request.OrderId,
                ["itemProductId"] = request.ItemProductId,
                ["status"] = request.Status
            }, request.UserId > 0 ? request.UserId : null);
            return Ok(request);
        }

        [HttpGet("my")]
        [Authorize]
        public async Task<IActionResult> GetMyReturns()
        {
            var requesterId = GetCurrentUserId();
            if (!requesterId.HasValue)
            {
                return Ok(new List<ReturnRequest>());
            }

            var items = await _db.ReturnRequests
                .Where(item => item.UserId == requesterId.Value)
                .OrderByDescending(item => item.CreatedAt)
                .ToListAsync();
            return Ok(items);
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetAllReturns()
        {
            var items = await _db.ReturnRequests
                .OrderByDescending(item => item.CreatedAt)
                .ToListAsync();
            return Ok(items);
        }

        [HttpPatch("{id:int}/status")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateReturnStatusRequest input)
        {
            if (input == null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "ReturnRequestStatusUpdate", "ReturnRequest", id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "missing_payload"
                });
                return BadRequest(new { message = "Status payload is required." });
            }

            var request = await _db.ReturnRequests.FirstOrDefaultAsync(item => item.Id == id);
            if (request == null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "ReturnRequestStatusUpdate", "ReturnRequest", id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "not_found"
                });
                return NotFound(new { message = "Return request not found." });
            }

            var nextStatus = (input.Status ?? "").Trim();
            if (!AllowedStatuses.Contains(nextStatus))
            {
                await _auditLogService.WriteAsync(User, HttpContext, "ReturnRequestStatusUpdate", "ReturnRequest", id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "invalid_status",
                    ["status"] = nextStatus
                }, request.UserId > 0 ? request.UserId : null);
                return BadRequest(new { message = "Invalid return status." });
            }

            var previousStatus = request.Status;
            request.Status = nextStatus;
            request.AdminRemark = (input.AdminRemark ?? "").Trim();
            request.UpdatedAt = DateTime.UtcNow;

            var order = await _db.Orders.FirstOrDefaultAsync(item => item.Id == request.OrderId);
            if (order != null)
            {
                if (string.Equals(nextStatus, "Approved", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(nextStatus, "Pickup Completed", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(nextStatus, "Completed", StringComparison.OrdinalIgnoreCase))
                {
                    order.Status = "Returned";
                    order.UpdatedAt = DateTime.UtcNow;
                }
                else if (string.Equals(nextStatus, "Refunded", StringComparison.OrdinalIgnoreCase))
                {
                    order.Status = "Refunded";
                    order.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _db.SaveChangesAsync();
            await _auditLogService.WriteAsync(User, HttpContext, "ReturnRequestStatusUpdate", "ReturnRequest", request.Id.ToString(), "Success", new Dictionary<string, object?>
            {
                ["fromStatus"] = previousStatus,
                ["toStatus"] = nextStatus
            }, request.UserId > 0 ? request.UserId : null);
            return Ok(request);
        }

        private int? GetCurrentUserId()
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(idClaim, out var parsed) ? parsed : null;
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
    }
}
