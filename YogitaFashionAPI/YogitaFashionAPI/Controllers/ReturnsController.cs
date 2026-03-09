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
            "Rejected",
            "Refunded"
        };

        private readonly AppDbContext _db;

        public ReturnsController(AppDbContext db)
        {
            _db = db;
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateReturn([FromBody] CreateReturnRequest input)
        {
            var requesterId = GetCurrentUserId();
            var role = User.FindFirstValue(ClaimTypes.Role) ?? "";
            if (!requesterId.HasValue && !string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized();
            }

            var order = await _db.Orders.FirstOrDefaultAsync(item => item.Id == input.OrderId);
            if (order == null)
            {
                return NotFound(new { message = "Order not found." });
            }

            if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) &&
                order.UserId > 0 &&
                requesterId.GetValueOrDefault() != order.UserId)
            {
                return Forbid();
            }

            if (!string.Equals(order.Status, "Delivered", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Return can only be requested after delivery." });
            }

            var productId = (input.ItemProductId ?? "").Trim();
            var orderItem = order.Items.FirstOrDefault(item => string.Equals(item.ProductId, productId, StringComparison.OrdinalIgnoreCase));
            if (orderItem == null)
            {
                return BadRequest(new { message = "Product not found in this order." });
            }

            var duplicate = await _db.ReturnRequests.FirstOrDefaultAsync(item =>
                item.OrderId == order.Id &&
                string.Equals(item.ItemProductId, productId, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(item.Status, "Rejected", StringComparison.OrdinalIgnoreCase));
            if (duplicate != null)
            {
                return Conflict(new { message = "Return request already exists for this item.", duplicate.Id });
            }

            var refundAmount = Math.Round(orderItem.Price * Math.Max(1, orderItem.Qty), 2);
            var request = new ReturnRequest
            {
                OrderId = order.Id,
                UserId = order.UserId,
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
            var request = await _db.ReturnRequests.FirstOrDefaultAsync(item => item.Id == id);
            if (request == null)
            {
                return NotFound(new { message = "Return request not found." });
            }

            var nextStatus = (input.Status ?? "").Trim();
            if (!AllowedStatuses.Contains(nextStatus))
            {
                return BadRequest(new { message = "Invalid return status." });
            }

            request.Status = nextStatus;
            request.AdminRemark = (input.AdminRemark ?? "").Trim();
            request.UpdatedAt = DateTime.UtcNow;

            var order = await _db.Orders.FirstOrDefaultAsync(item => item.Id == request.OrderId);
            if (order != null)
            {
                if (string.Equals(nextStatus, "Approved", StringComparison.OrdinalIgnoreCase))
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
            AuditLogStore.Add(User, "UpdateStatus", "ReturnRequest", request.Id.ToString(), $"Changed return status to {nextStatus}.");
            return Ok(request);
        }

        private int? GetCurrentUserId()
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(idClaim, out var parsed) ? parsed : null;
        }
    }
}
