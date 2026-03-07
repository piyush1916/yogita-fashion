using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YogitaFashionAPI.Models;
using YogitaFashionAPI.Services;

namespace YogitaFashionAPI.Controllers
{
    [Route("coupons")]
    [ApiController]
    public class CouponsController : ControllerBase
    {
        public static List<Coupon> CouponStore => coupons;
        public static List<CouponUsageRecord> CouponUsageStore => usageRecords;

        private static readonly List<Coupon> coupons = new()
        {
            new Coupon
            {
                Id = 1,
                Code = "SAVE10",
                Type = "percent",
                Value = 10,
                MinOrderAmount = 499,
                MaxUses = 1000,
                MaxUsesPerUser = 2,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
            new Coupon
            {
                Id = 2,
                Code = "FASHION200",
                Type = "fixed",
                Value = 200,
                MinOrderAmount = 1499,
                MaxUses = 500,
                MaxUsesPerUser = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }
        };

        private static readonly List<CouponUsageRecord> usageRecords = new();

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult GetCoupons()
        {
            return Ok(coupons.OrderByDescending(item => item.UpdatedAt).ToList());
        }

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult CreateCoupon([FromBody] Coupon input)
        {
            if (input == null)
            {
                return BadRequest(new { message = "Coupon payload is required." });
            }

            var normalized = NormalizeCoupon(input);
            if (string.IsNullOrWhiteSpace(normalized.Code))
            {
                return BadRequest(new { message = "Coupon code is required." });
            }

            if (normalized.StartAt.HasValue && normalized.EndAt.HasValue && normalized.StartAt > normalized.EndAt)
            {
                return BadRequest(new { message = "End date must be greater than start date." });
            }

            if (coupons.Any(item => string.Equals(item.Code, normalized.Code, StringComparison.OrdinalIgnoreCase)))
            {
                return Conflict(new { message = "Coupon code already exists." });
            }

            normalized.Id = coupons.Count == 0 ? 1 : coupons.Max(item => item.Id) + 1;
            normalized.CreatedAt = DateTime.UtcNow;
            normalized.UpdatedAt = DateTime.UtcNow;
            coupons.Add(normalized);

            AuditLogStore.Add(User, "Create", "Coupon", normalized.Id.ToString(), $"Created coupon {normalized.Code}.");
            return Ok(normalized);
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult UpdateCoupon(int id, [FromBody] Coupon input)
        {
            if (input == null)
            {
                return BadRequest(new { message = "Coupon payload is required." });
            }

            var existing = coupons.FirstOrDefault(item => item.Id == id);
            if (existing == null)
            {
                return NotFound(new { message = "Coupon not found." });
            }

            var normalized = NormalizeCoupon(input);
            if (string.IsNullOrWhiteSpace(normalized.Code))
            {
                return BadRequest(new { message = "Coupon code is required." });
            }

            if (normalized.StartAt.HasValue && normalized.EndAt.HasValue && normalized.StartAt > normalized.EndAt)
            {
                return BadRequest(new { message = "End date must be greater than start date." });
            }

            var duplicate = coupons.FirstOrDefault(item =>
                item.Id != id && string.Equals(item.Code, normalized.Code, StringComparison.OrdinalIgnoreCase));
            if (duplicate != null)
            {
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

            AuditLogStore.Add(User, "Update", "Coupon", existing.Id.ToString(), $"Updated coupon {existing.Code}.");
            return Ok(existing);
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult DeleteCoupon(int id)
        {
            var existing = coupons.FirstOrDefault(item => item.Id == id);
            if (existing == null)
            {
                return NotFound(new { message = "Coupon not found." });
            }

            coupons.Remove(existing);
            usageRecords.RemoveAll(item => item.CouponId == id);
            AuditLogStore.Add(User, "Delete", "Coupon", existing.Id.ToString(), $"Deleted coupon {existing.Code}.");
            return NoContent();
        }

        [HttpPost("validate")]
        [AllowAnonymous]
        public IActionResult ValidateCoupon([FromBody] JsonElement payload)
        {
            var request = ParseValidationRequest(payload);
            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(new { message = "Coupon code is required." });
            }

            var code = request.Code.Trim().ToUpperInvariant();
            var subtotal = Math.Max(0, request.Subtotal);
            var coupon = coupons.FirstOrDefault(item => string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase));
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
            if (userId > 0 && coupon.MaxUsesPerUser > 0)
            {
                var usage = usageRecords.FirstOrDefault(item => item.CouponId == coupon.Id && item.UserId == userId);
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

            coupon.UsedCount += 1;
            coupon.UpdatedAt = DateTime.UtcNow;

            if (userId > 0)
            {
                var usage = usageRecords.FirstOrDefault(item => item.CouponId == coupon.Id && item.UserId == userId);
                if (usage == null)
                {
                    usageRecords.Add(new CouponUsageRecord
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
