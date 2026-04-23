using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YogitaFashionAPI.Data;
using YogitaFashionAPI.Models;
using YogitaFashionAPI.Services;

namespace YogitaFashionAPI.Controllers
{
    [Route("coupons")]
    [ApiController]
    public class CouponsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IAuditLogService _auditLogService;

        public CouponsController(AppDbContext db, IAuditLogService auditLogService)
        {
            _db = db;
            _auditLogService = auditLogService;
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetCoupons()
        {
            var items = await _db.Coupons
                .OrderByDescending(item => item.UpdatedAt)
                .ToListAsync();
            return Ok(items);
        }

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> CreateCoupon([FromBody] Coupon input)
        {
            if (input == null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "CouponCreate", "Coupon", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "missing_payload"
                });
                return BadRequest(new { message = "Coupon payload is required." });
            }

            var normalized = NormalizeCoupon(input);
            if (string.IsNullOrWhiteSpace(normalized.Code))
            {
                await _auditLogService.WriteAsync(User, HttpContext, "CouponCreate", "Coupon", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "missing_code"
                });
                return BadRequest(new { message = "Coupon code is required." });
            }

            if (normalized.StartAt.HasValue && normalized.EndAt.HasValue && normalized.StartAt > normalized.EndAt)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "CouponCreate", "Coupon", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "invalid_date_range",
                    ["code"] = normalized.Code
                });
                return BadRequest(new { message = "End date must be greater than start date." });
            }

            var duplicate = await _db.Coupons.AnyAsync(item =>
                (item.Code ?? "").ToUpper() == normalized.Code);
            if (duplicate)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "CouponCreate", "Coupon", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "duplicate_code",
                    ["code"] = normalized.Code
                });
                return Conflict(new { message = "Coupon code already exists." });
            }

            var now = DateTime.UtcNow;
            normalized.CreatedAt = now;
            normalized.UpdatedAt = now;

            _db.Coupons.Add(normalized);
            await _db.SaveChangesAsync();

            await _auditLogService.WriteAsync(User, HttpContext, "CouponCreate", "Coupon", normalized.Id.ToString(), "Success", new Dictionary<string, object?>
            {
                ["code"] = normalized.Code,
                ["type"] = normalized.Type,
                ["isActive"] = normalized.IsActive
            });
            return Ok(normalized);
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> UpdateCoupon(int id, [FromBody] Coupon input)
        {
            if (input == null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "CouponUpdate", "Coupon", id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "missing_payload"
                });
                return BadRequest(new { message = "Coupon payload is required." });
            }

            var existing = await _db.Coupons.FirstOrDefaultAsync(item => item.Id == id);
            if (existing == null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "CouponUpdate", "Coupon", id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "not_found"
                });
                return NotFound(new { message = "Coupon not found." });
            }

            var normalized = NormalizeCoupon(input);
            if (string.IsNullOrWhiteSpace(normalized.Code))
            {
                await _auditLogService.WriteAsync(User, HttpContext, "CouponUpdate", "Coupon", id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "missing_code"
                });
                return BadRequest(new { message = "Coupon code is required." });
            }

            if (normalized.StartAt.HasValue && normalized.EndAt.HasValue && normalized.StartAt > normalized.EndAt)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "CouponUpdate", "Coupon", id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "invalid_date_range",
                    ["code"] = normalized.Code
                });
                return BadRequest(new { message = "End date must be greater than start date." });
            }

            var duplicate = await _db.Coupons.FirstOrDefaultAsync(item =>
                item.Id != id && (item.Code ?? "").ToUpper() == normalized.Code);
            if (duplicate != null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "CouponUpdate", "Coupon", id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "duplicate_code",
                    ["code"] = normalized.Code
                });
                return Conflict(new { message = "Coupon code already exists." });
            }

            existing.Code = normalized.Code;
            existing.Type = normalized.Type;
            existing.Value = normalized.Value;
            existing.MinOrderAmount = normalized.MinOrderAmount;
            existing.MaxUses = normalized.MaxUses;
            existing.MaxUsesPerUser = normalized.MaxUsesPerUser;
            existing.IsActive = normalized.IsActive;
            existing.StartAt = normalized.StartAt;
            existing.EndAt = normalized.EndAt;
            existing.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            await _auditLogService.WriteAsync(User, HttpContext, "CouponUpdate", "Coupon", existing.Id.ToString(), "Success", new Dictionary<string, object?>
            {
                ["code"] = existing.Code,
                ["type"] = existing.Type,
                ["isActive"] = existing.IsActive
            });
            return Ok(existing);
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> DeleteCoupon(int id)
        {
            var existing = await _db.Coupons.FirstOrDefaultAsync(item => item.Id == id);
            if (existing == null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "CouponDelete", "Coupon", id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "not_found"
                });
                return NotFound(new { message = "Coupon not found." });
            }

            var usage = await _db.CouponUsageRecords
                .Where(item => item.CouponId == id)
                .ToListAsync();

            if (usage.Count > 0)
            {
                _db.CouponUsageRecords.RemoveRange(usage);
            }

            _db.Coupons.Remove(existing);
            await _db.SaveChangesAsync();

            await _auditLogService.WriteAsync(User, HttpContext, "CouponDelete", "Coupon", existing.Id.ToString(), "Success", new Dictionary<string, object?>
            {
                ["code"] = existing.Code
            });
            return NoContent();
        }

        [HttpPost("validate")]
        [AllowAnonymous]
        public async Task<IActionResult> ValidateCoupon([FromBody] JsonElement payload)
        {
            var request = ParseValidationRequest(payload);
            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(new { message = "Coupon code is required." });
            }

            var code = request.Code.Trim().ToUpperInvariant();
            var subtotal = Math.Max(0, request.Subtotal);
            var coupon = await _db.Coupons.FirstOrDefaultAsync(item =>
                (item.Code ?? "").ToUpper() == code);
            if (coupon == null)
            {
                return NotFound("Invalid coupon");
            }

            if (!coupon.IsActive)
            {
                return BadRequest(new CouponValidationResult
                {
                    Valid = false,
                    Code = code,
                    Message = "Coupon is inactive."
                });
            }

            var now = DateTime.UtcNow;
            if (coupon.StartAt.HasValue && ToUtc(coupon.StartAt.Value) > now)
            {
                return BadRequest(new CouponValidationResult
                {
                    Valid = false,
                    Code = code,
                    Message = "Coupon is not active yet."
                });
            }

            if (coupon.EndAt.HasValue && ToUtc(coupon.EndAt.Value) < now)
            {
                return BadRequest(new CouponValidationResult
                {
                    Valid = false,
                    Code = code,
                    Message = "Coupon has expired."
                });
            }

            if (coupon.MaxUses > 0 && coupon.UsedCount >= coupon.MaxUses)
            {
                return BadRequest(new CouponValidationResult
                {
                    Valid = false,
                    Code = code,
                    Message = "Coupon usage limit reached."
                });
            }

            if (subtotal < coupon.MinOrderAmount)
            {
                return BadRequest(new CouponValidationResult
                {
                    Valid = false,
                    Code = code,
                    Message = $"Minimum order value Rs {coupon.MinOrderAmount} required."
                });
            }

            var userId = Math.Max(0, request.UserId);
            CouponUsageRecord? usage = null;
            if (userId > 0 && coupon.MaxUsesPerUser > 0)
            {
                usage = await _db.CouponUsageRecords.FirstOrDefaultAsync(item =>
                    item.CouponId == coupon.Id && item.UserId == userId);

                if (usage != null && usage.Count >= coupon.MaxUsesPerUser)
                {
                    return BadRequest(new CouponValidationResult
                    {
                        Valid = false,
                        Code = code,
                        Message = "You have already used this coupon the maximum number of times."
                    });
                }
            }

            var discountAmount = CalculateDiscount(coupon, subtotal);
            var discountPercent = subtotal <= 0 ? 0 : Math.Round((discountAmount / subtotal) * 100, 2);
            var finalTotal = Math.Max(0, subtotal - discountAmount);

            return Ok(new CouponValidationResult
            {
                Valid = true,
                Code = code,
                DiscountAmount = discountAmount,
                DiscountPercent = discountPercent,
                FinalTotal = finalTotal,
                Message = "Coupon applied successfully."
            });
        }

        private static Coupon NormalizeCoupon(Coupon input)
        {
            var type = string.Equals(input.Type, "fixed", StringComparison.OrdinalIgnoreCase) ? "fixed" : "percent";
            return new Coupon
            {
                Code = (input.Code ?? "").Trim().ToUpperInvariant(),
                Type = type,
                Value = Math.Max(0, input.Value),
                MinOrderAmount = Math.Max(0, input.MinOrderAmount),
                MaxUses = Math.Max(0, input.MaxUses),
                MaxUsesPerUser = Math.Max(0, input.MaxUsesPerUser),
                IsActive = input.IsActive,
                StartAt = input.StartAt.HasValue ? ToUtc(input.StartAt.Value) : null,
                EndAt = input.EndAt.HasValue ? ToUtc(input.EndAt.Value) : null,
                UsedCount = Math.Max(0, input.UsedCount),
            };
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

        private static CouponValidationRequest ParseValidationRequest(JsonElement payload)
        {
            if (payload.ValueKind == JsonValueKind.String)
            {
                return new CouponValidationRequest { Code = payload.GetString() ?? "" };
            }

            if (payload.ValueKind != JsonValueKind.Object)
            {
                return new CouponValidationRequest();
            }

            string code = "";
            var subtotal = 0m;
            var userId = 0;

            if (payload.TryGetProperty("code", out var codeNode))
            {
                code = codeNode.GetString() ?? "";
            }
            else if (payload.TryGetProperty("Code", out var codeNodeAlt))
            {
                code = codeNodeAlt.GetString() ?? "";
            }

            if (payload.TryGetProperty("subtotal", out var subtotalNode))
            {
                subtotal = ParseDecimal(subtotalNode);
            }
            else if (payload.TryGetProperty("Subtotal", out var subtotalNodeAlt))
            {
                subtotal = ParseDecimal(subtotalNodeAlt);
            }

            if (payload.TryGetProperty("userId", out var userNode))
            {
                userId = ParseInt(userNode);
            }
            else if (payload.TryGetProperty("UserId", out var userNodeAlt))
            {
                userId = ParseInt(userNodeAlt);
            }

            return new CouponValidationRequest
            {
                Code = code,
                UserId = userId,
                Subtotal = subtotal
            };
        }

        private static int ParseInt(JsonElement node)
        {
            return node.ValueKind switch
            {
                JsonValueKind.Number when node.TryGetInt32(out var value) => value,
                JsonValueKind.String when int.TryParse(node.GetString(), out var value) => value,
                _ => 0
            };
        }

        private static decimal ParseDecimal(JsonElement node)
        {
            return node.ValueKind switch
            {
                JsonValueKind.Number when node.TryGetDecimal(out var value) => value,
                JsonValueKind.String when decimal.TryParse(node.GetString(), out var value) => value,
                _ => 0m
            };
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
    }
}
