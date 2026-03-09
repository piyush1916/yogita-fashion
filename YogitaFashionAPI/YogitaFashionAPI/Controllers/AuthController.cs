using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YogitaFashionAPI.Data;
using YogitaFashionAPI.Models;
using YogitaFashionAPI.Services;

namespace YogitaFashionAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _db;

        public AuthController(IConfiguration configuration, AppDbContext db)
        {
            _configuration = configuration;
            _db = db;
        }

        [HttpGet("users")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetUsers()
        {
            var items = await _db.Users
                .OrderByDescending(user => user.CreatedAt)
                .Select(user => ToPublicUser(user))
                .ToListAsync();

            return Ok(items);
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] User input)
        {
            var email = (input.Email ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(input.Password))
            {
                return BadRequest("Email and password are required.");
            }

            var alreadyExists = await _db.Users.AnyAsync(existing => string.Equals(existing.Email, email, StringComparison.OrdinalIgnoreCase));
            if (alreadyExists)
            {
                return Conflict("Email already registered.");
            }

            var requesterRole = User.FindFirstValue(ClaimTypes.Role) ?? "";
            var requestedRole = (input.Role ?? "").Trim();
            var role = string.Equals(requesterRole, "Admin", StringComparison.OrdinalIgnoreCase) &&
                       !string.IsNullOrWhiteSpace(requestedRole)
                ? requestedRole
                : "Customer";

            var user = new User
            {
                Name = (input.Name ?? "").Trim(),
                Email = email,
                Password = input.Password,
                Phone = (input.Phone ?? "").Trim(),
                City = (input.City ?? "").Trim(),
                Role = role,
                CreatedAt = input.CreatedAt == default ? DateTime.UtcNow : input.CreatedAt
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var token = JwtTokenService.GenerateToken(user, _configuration);
            return Ok(new
            {
                token,
                user = ToPublicUser(user)
            });
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] User loginUser)
        {
            var email = (loginUser.Email ?? "").Trim().ToLowerInvariant();
            var password = loginUser.Password ?? "";

            var user = await _db.Users.FirstOrDefaultAsync(existing =>
                string.Equals(existing.Email, email, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.Password, password, StringComparison.Ordinal));

            if (user == null)
            {
                return Unauthorized("Invalid email or password");
            }

            var token = JwtTokenService.GenerateToken(user, _configuration);
            return Ok(new
            {
                token,
                user = ToPublicUser(user)
            });
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var user = await _db.Users.FirstOrDefaultAsync(existing => existing.Id == currentUserId.Value);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            return Ok(ToPublicUser(user));
        }

        [HttpGet("profile/{id:int}")]
        [Authorize]
        public async Task<IActionResult> GetProfile(int id)
        {
            if (!CanAccessUser(id))
            {
                return Forbid();
            }

            var user = await _db.Users.FirstOrDefaultAsync(existing => existing.Id == id);
            if (user == null)
            {
                return NotFound();
            }

            return Ok(ToPublicUser(user));
        }

        [HttpPut("profile/{id:int}")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile(int id, [FromBody] User payload)
        {
            if (!CanAccessUser(id))
            {
                return Forbid();
            }

            var user = await _db.Users.FirstOrDefaultAsync(existing => existing.Id == id);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            var nextEmail = string.IsNullOrWhiteSpace(payload.Email)
                ? user.Email
                : payload.Email.Trim().ToLowerInvariant();

            var duplicate = await _db.Users.FirstOrDefaultAsync(existing =>
                existing.Id != id &&
                string.Equals(existing.Email, nextEmail, StringComparison.OrdinalIgnoreCase));

            if (duplicate != null)
            {
                return Conflict("Email already registered.");
            }

            user.Name = string.IsNullOrWhiteSpace(payload.Name) ? user.Name : payload.Name.Trim();
            user.Email = nextEmail;
            user.Phone = payload.Phone?.Trim() ?? user.Phone;
            user.City = payload.City?.Trim() ?? user.City;

            var requesterRole = User.FindFirstValue(ClaimTypes.Role) ?? "";
            if (string.Equals(requesterRole, "Admin", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(payload.Role))
            {
                user.Role = payload.Role.Trim();
            }

            if (!string.IsNullOrWhiteSpace(payload.Password))
            {
                user.Password = payload.Password;
            }

            await _db.SaveChangesAsync();
            return Ok(ToPublicUser(user));
        }

        private bool CanAccessUser(int targetUserId)
        {
            var requesterRole = User.FindFirstValue(ClaimTypes.Role) ?? "";
            if (string.Equals(requesterRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var requesterId = GetCurrentUserId();
            return requesterId.HasValue && requesterId.Value == targetUserId;
        }

        private int? GetCurrentUserId()
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(idClaim, out var id) ? id : null;
        }

        private static object ToPublicUser(User user)
        {
            return new
            {
                user.Id,
                user.Name,
                user.Email,
                user.Phone,
                user.City,
                user.Role,
                user.CreatedAt
            };
        }
    }
}
