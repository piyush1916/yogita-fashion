using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YogitaFashionAPI.Models;
using YogitaFashionAPI.Services;

namespace YogitaFashionAPI.Controllers
{
    [ApiController]
    [Route("returns")]
    public class ReturnsController : ControllerBase
    {
        public static List<ReturnRequest> ReturnStore => requests;

        private static readonly List<ReturnRequest> requests = new();
        private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Pending",
            "Approved",
            "Rejected",
            "Refunded"
        };

        [HttpPost]
        [Authorize]
        public IActionResult CreateReturn([FromBody] CreateReturnRequest input)
        {
            var requesterId = GetCurrentUserId();
            var role = User.FindFirstValue(ClaimTypes.Role) ?? "";
            if (!requesterId.HasValue && !string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized();
            }

            var order = OrdersController.OrderStore.FirstOrDefault(item => item.Id == input.OrderId);
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

            var duplicate = requests.FirstOrDefault(item =>
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
                Id = requests.Count == 0 ? 1 : requests.Max(item => item.Id) + 1,
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

            requests.Add(request);
            return Ok(request);
        }

        [HttpGet("my")]
        [Authorize]
        public IActionResult GetMyReturns()
        {
            var requesterId = GetCurrentUserId();
            if (!requesterId.HasValue)
            {
                return Ok(new List<ReturnRequest>());
            }

            var items = requests
                .Where(item => item.UserId == requesterId.Value)
                .OrderByDescending(item => item.CreatedAt)
                .ToList();
            return Ok(items);
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult GetAllReturns()
        {
            return Ok(requests.OrderByDescending(item => item.CreatedAt).ToList());
        }

        [HttpPatch("{id:int}/status")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult UpdateStatus(int id, [FromBody] UpdateReturnStatusRequest input)
        {
            var request = requests.FirstOrDefault(item => item.Id == id);
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

            var order = OrdersController.OrderStore.FirstOrDefault(item => item.Id == request.OrderId);
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
