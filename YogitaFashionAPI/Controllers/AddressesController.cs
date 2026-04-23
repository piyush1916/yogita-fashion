using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YogitaFashionAPI.Data;
using YogitaFashionAPI.Models;
using YogitaFashionAPI.Services;

namespace YogitaFashionAPI.Controllers
{
    [Route("addresses")]
    [ApiController]
    [Authorize]
    public class AddressesController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IAuditLogService _auditLogService;

        public AddressesController(AppDbContext db, IAuditLogService auditLogService)
        {
            _db = db;
            _auditLogService = auditLogService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAddresses([FromQuery] int? userId = null)
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

            var items = await _db.Addresses
                .Where(address => address.UserId == targetUserId)
                .OrderByDescending(address => address.Id)
                .ToListAsync();
            return Ok(items);
        }

        [HttpPost]
        public async Task<IActionResult> AddAddress([FromBody] Address input)
        {
            if (input == null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "AddressAdd", "Address", status: "Failure", metadata: new Dictionary<string, object?>
                {
                    ["reason"] = "missing_payload"
                });
                return BadRequest("Address payload is required.");
            }

            var requesterId = GetCurrentUserId();
            if (!requesterId.HasValue)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "AddressAdd", "Address", status: "Failure", metadata: new Dictionary<string, object?>
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

            var address = new Address
            {
                UserId = userId,
                FullName = (input.FullName ?? "").Trim(),
                Phone = (input.Phone ?? "").Trim(),
                City = (input.City ?? "").Trim(),
                State = (input.State ?? "").Trim(),
                Pincode = (input.Pincode ?? "").Trim(),
                Country = string.IsNullOrWhiteSpace(input.Country) ? "India" : input.Country.Trim(),
                Street = (input.Street ?? "").Trim(),
                Line2 = (input.Line2 ?? "").Trim(),
                Landmark = (input.Landmark ?? "").Trim(),
                AddressType = string.IsNullOrWhiteSpace(input.AddressType) ? "Home" : input.AddressType.Trim(),
                IsDefault = input.IsDefault,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            if (address.IsDefault)
            {
                var defaults = await _db.Addresses.Where(item => item.UserId == userId && item.IsDefault).ToListAsync();
                foreach (var existing in defaults)
                {
                    existing.IsDefault = false;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
            }

            _db.Addresses.Add(address);
            await _db.SaveChangesAsync();
            await _auditLogService.WriteAsync(User, HttpContext, "AddressAdd", "Address", address.Id.ToString(), "Success", new Dictionary<string, object?>
            {
                ["targetUserId"] = address.UserId,
                ["addressType"] = address.AddressType,
                ["isDefault"] = address.IsDefault
            });
            return Ok(address);
        }

        [HttpPatch("{id:int}")]
        public async Task<IActionResult> UpdateAddress(int id, [FromBody] Address input)
        {
            if (input == null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "AddressUpdate", "Address", id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "missing_payload"
                });
                return BadRequest("Address payload is required.");
            }

            var requesterId = GetCurrentUserId();
            if (!requesterId.HasValue)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "AddressUpdate", "Address", id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "unauthorized"
                });
                return Unauthorized();
            }

            var address = await _db.Addresses.FirstOrDefaultAsync(item => item.Id == id);
            if (address == null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "AddressUpdate", "Address", id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "not_found"
                });
                return NotFound("Address not found");
            }

            if (!IsAdmin() && address.UserId != requesterId.Value)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "AddressUpdate", "Address", id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "forbidden"
                });
                return Forbid();
            }

            address.FullName = (input.FullName ?? "").Trim();
            address.Phone = (input.Phone ?? "").Trim();
            address.City = (input.City ?? "").Trim();
            address.State = (input.State ?? "").Trim();
            address.Pincode = (input.Pincode ?? "").Trim();
            address.Country = string.IsNullOrWhiteSpace(input.Country) ? "India" : input.Country.Trim();
            address.Street = (input.Street ?? "").Trim();
            address.Line2 = (input.Line2 ?? "").Trim();
            address.Landmark = (input.Landmark ?? "").Trim();
            address.AddressType = string.IsNullOrWhiteSpace(input.AddressType) ? "Home" : input.AddressType.Trim();
            address.IsDefault = input.IsDefault;
            address.UpdatedAt = DateTime.UtcNow;

            if (IsAdmin() && input.UserId > 0)
            {
                address.UserId = input.UserId;
            }

            if (address.IsDefault)
            {
                var defaults = await _db.Addresses
                    .Where(item => item.UserId == address.UserId && item.Id != address.Id && item.IsDefault)
                    .ToListAsync();
                foreach (var existing in defaults)
                {
                    existing.IsDefault = false;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _db.SaveChangesAsync();
            await _auditLogService.WriteAsync(User, HttpContext, "AddressUpdate", "Address", address.Id.ToString(), "Success", new Dictionary<string, object?>
            {
                ["targetUserId"] = address.UserId,
                ["addressType"] = address.AddressType,
                ["isDefault"] = address.IsDefault
            });
            return Ok(address);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteAddress(int id)
        {
            var requesterId = GetCurrentUserId();
            if (!requesterId.HasValue)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "AddressDelete", "Address", id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "unauthorized"
                });
                return Unauthorized();
            }

            var address = await _db.Addresses.FirstOrDefaultAsync(item => item.Id == id);
            if (address == null)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "AddressDelete", "Address", id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "not_found"
                });
                return NotFound("Address not found");
            }

            if (!IsAdmin() && address.UserId != requesterId.Value)
            {
                await _auditLogService.WriteAsync(User, HttpContext, "AddressDelete", "Address", id.ToString(), "Failure", new Dictionary<string, object?>
                {
                    ["reason"] = "forbidden"
                });
                return Forbid();
            }

            _db.Addresses.Remove(address);
            await _db.SaveChangesAsync();
            await _auditLogService.WriteAsync(User, HttpContext, "AddressDelete", "Address", address.Id.ToString(), "Success", new Dictionary<string, object?>
            {
                ["targetUserId"] = address.UserId
            });
            return Ok(new { message = "Address deleted successfully" });
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
