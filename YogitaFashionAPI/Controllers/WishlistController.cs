using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YogitaFashionAPI.Data;
using YogitaFashionAPI.Models;
using YogitaFashionAPI.Services;

namespace YogitaFashionAPI.Controllers
{
    [Route("wishlist")]
    [ApiController]
    [Authorize]
    public class WishlistController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IAuditLogService _auditLogService;

        public WishlistController(AppDbContext db, IAuditLogService auditLogService)
        {
            _db = db;
            _auditLogService = auditLogService;
        }

        [HttpGet]
        public async Task<IActionResult> GetWishlist([FromQuery] int? userId = null)
        {
            var requesterId = GetCurrentUserId();
            if (!requesterId.HasValue)
            {
                return Unauthorized();
            }

            var targetUserId = requesterId.Value;
            if (IsAdmin() && userId.HasValue && userId.Value > 0)
            {
                targetUserId = userId.Value;
            }

            var items = await _db.WishlistItems
                .Where(item => item.UserId == targetUserId)
                .OrderByDescending(item => item.Id)
                .ToListAsync();
            return Ok(items);
        }

        [HttpPost]
        public async Task<IActionResult> AddToWishlist([FromBody] WishlistItem input)
        {
            if (input == null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "WishlistAdd", "Wishlist", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "missing_payload"
                });
                return BadRequest("Wishlist payload is required.");
            }

            if (input.ProductId <= 0)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "WishlistAdd", "Wishlist", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "invalid_product_id"
                });
                return BadRequest("Valid productId is required.");
            }

            var requesterId = GetCurrentUserId();
            if (!requesterId.HasValue)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "WishlistAdd", "Wishlist", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "unauthorized"
                });
                return Unauthorized();
            }

            var userId = requesterId.Value;
            if (IsAdmin() && input.UserId > 0)
            {
                userId = input.UserId;
            }

            var existing = await _db.WishlistItems.FirstOrDefaultAsync(item =>
                item.UserId == userId && item.ProductId == input.ProductId);
            if (existing != null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "WishlistAdd", "Wishlist", existing.Id.ToString(), "Success", new Dictionary<string, object?>
                {
                    ["productId"] = existing.ProductId,
                    ["targetUserId"] = existing.UserId,
                    ["alreadyExists"] = true
                });
                return Ok(existing);
            }

            var item = new WishlistItem
            {
                UserId = userId,
                ProductId = input.ProductId,
                CreatedAt = DateTime.UtcNow
            };

            _db.WishlistItems.Add(item);
            await _db.SaveChangesAsync();
            await _auditLogService.WriteAsync(User, HttpContext, "WishlistAdd", "Wishlist", item.Id.ToString(), "Success", new Dictionary<string, object?>
            {
                ["productId"] = item.ProductId,
                ["targetUserId"] = item.UserId
            });
            return Ok(item);
        }

        [HttpDelete("{productId:int}")]
        public async Task<IActionResult> RemoveFromWishlist(int productId, [FromQuery] int? userId = null)
        {
            var requesterId = GetCurrentUserId();
            if (!requesterId.HasValue)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "WishlistRemove", "Wishlist", productId.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "unauthorized"
                });
                return Unauthorized();
            }

            var targetUserId = requesterId.Value;
            if (IsAdmin() && userId.HasValue && userId.Value > 0)
            {
                targetUserId = userId.Value;
            }

            var item = await _db.WishlistItems.FirstOrDefaultAsync(entry =>
                entry.ProductId == productId && entry.UserId == targetUserId);
            if (item == null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "WishlistRemove", "Wishlist", productId.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "not_found",
                    ["targetUserId"] = targetUserId
                });
                return NotFound("Wishlist item not found");
            }

            _db.WishlistItems.Remove(item);
            await _db.SaveChangesAsync();
            await _auditLogService.WriteAsync(User, HttpContext, "WishlistRemove", "Wishlist", item.Id.ToString(), "Success", new Dictionary<string, object?>
            {
                ["productId"] = item.ProductId,
                ["targetUserId"] = item.UserId
            });
            return Ok(new { message = "Removed from wishlist" });
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
